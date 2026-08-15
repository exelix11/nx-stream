using System;
using osum.Helpers;
using osum.Audio;
using OpenTK.Audio.OpenAL;
using OpenTK.Audio;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace osum
{
	public static class OpenALSharedContext
	{
		private static AudioContext _context;

		public static int ReservedSources = 1;

		public static AudioContext Context => _context ??= new AudioContext();
	}

	record class TrackBuffer(string Key, int SampleRate, int Channels, long TotalSamples, bool Looping, float Volume, float Pitch) : IDisposable
	{
		public required IntPtr Body { get; set; }

		public float DurationSeconds => (float)TotalSamples / SampleRate;
		int bufferId;

		public int GetOrLoadBufferId()
		{
			if (bufferId == 0)
				CreateOpenALBuffer();

			return bufferId;
		}

		public void CreateOpenALBuffer()
		{
			if (bufferId != 0) throw new Exception($"Buffer already created for {Key}");
			if (Body == IntPtr.Zero) throw new Exception($"Buffer already used for {Key}");

			var alBuffer = AL.GenBuffer();
			if (alBuffer == 0)
			{
				BackgroundAudioPlayerLibnx.ALAssertError();
				throw new Exception($"Failed to generate OpenAL buffer for {Key}");
			}
			try
			{
				ALFormat format = Channels switch
				{
					1 => ALFormat.Mono16,
					2 => ALFormat.Stereo16,
					_ => throw new NotImplementedException($"Invalid format: 16 bits, {Channels} channels"),
				};

				var bytes = checked((int)(TotalSamples * Channels * (16 /*bitsPerSample*/ / 8)));

				AL.BufferData(alBuffer, format, Body, bytes, SampleRate);
				BackgroundAudioPlayerLibnx.ALAssertError();

				AL.GetBuffer(alBuffer, ALGetBufferi.Size, out int bufferSize);
				BackgroundAudioPlayerLibnx.ALAssertError();

				if (bufferSize != bytes)
					throw new Exception($"Buffer size mismatch for {Key}: expected {bytes}, got {bufferSize}");

				bufferId = alBuffer;
			}
			catch
			{
				AL.DeleteBuffer(alBuffer);
				throw;
			}
			finally
			{
				ReleaseNativeMemory();
			}
		}

		void ReleaseNativeMemory()
		{
			if (Body != IntPtr.Zero)
			{
				BackgroundAudioPlayerLibnx.libc_free(Body);
				Body = IntPtr.Zero;
			}
		}

		public void Dispose()
		{
			ReleaseNativeMemory();
			if (bufferId != 0)
			{
				AL.DeleteBuffer(bufferId);
				bufferId = 0;
			}
		}
	}

	public class BackgroundAudioPlayerLibnx : BackgroundAudioPlayer
	{
		BackgroundSourceAL ActiveTrack;
		Dictionary<string, TrackBuffer> CacheTracks = new();

		// Trick to make the menu feel more responsive: cache the song select music so the back button doesn't stutter.
		bool ShouldCacheSong(string song) => song == "Skins/Default/songselect.mp3";

		// Trick to make track selection feel more responsive: after a map is selected, load the song asynchronously
		Task<TrackBuffer> LoadingTask;
		// Also forward any seek or play requests to the track once it is loaded. Null means do not play, a value seeks and plays from there.
		int? PostLoadSeek;

		// Provided by the mono-nx fork in this repo
		[DllImport("osu_internal")]
		internal extern static void libc_free(IntPtr ptr);

		[DllImport("osu_internal")]
		[return: MarshalAs(UnmanagedType.LPStr)]
		extern static string? decode_audio_mp3(byte[] inputData, int inputLength,
			out IntPtr outputData, out int outputSamples,
			out int sampleRate, out int bitsPerSample, out int channels);

		[DllImport("osu_internal")]
		[return: MarshalAs(UnmanagedType.LPStr)]
		extern static string? decode_audio_mp4(byte[] inputData, int inputLength,
			out IntPtr outputData, out int outputSamples,
			out int sampleRate, out int bitsPerSample, out int channels);

		public BackgroundAudioPlayerLibnx()
		{
			var context = OpenALSharedContext.Context;
			ActiveTrack = new(AL.GenSources(1)[0]);
		}

		protected override void updateVolume()
		{
			ActiveTrack.Volume = pMathHelper.ClampToOne(DimmableVolume * MaxVolume);
		}

		public override float CurrentPower => .5f; // TODO

		public override bool Prepare()
		{
			return true;
		}

		public override void Update()
		{
			// Even though we started loading asynchronously, we need to finalize the load on the main thread because OpenAL is not thread-safe.
			// Read the thread safety notes below.
			if (LoadingTask != null && LoadingTask.IsCompleted)
			{
				try
				{
					var newTrack = LoadingTask.Result;
					FinalizeTrackLoad(newTrack, false);

					if (PostLoadSeek.HasValue)
					{
						SeekToInternal(PostLoadSeek.Value, false);
						ActiveTrack.Play();
					}
					else
					{
						ActiveTrack.Pause();
					}
				}
				catch (Exception e)
				{
					Logging.Write($"Error loading background audio: {e}");
					Unload();
				}
				finally
				{
					PostLoadSeek = null;
					LoadingTask = null;
				}
			}
		}

		public static void ALAssertError()
		{
			var err = AL.GetError();
			if (err != ALError.NoError)
				throw new Exception($"OpenAL error: {err}");
		}

		void StopPendingLoadingSongs()
		{
			if (LoadingTask != null)
			{
				// We also want the last load to always win in case there is one pending stop it here and start a new one.
				// Note that this only works because:
				// 1. The only method allowed to consume the loading task is Update() which is called on the main thread.
				// 2. The only methods allowed to invoke more loading are also called on the main thread.
				_ = LoadingTask.ContinueWith(static t =>
				{
					if (t.IsCompletedSuccessfully)
					{
						Logging.Write($"Dropping song that was loaded too late: {t.Result.Key}");
						t.Result.Dispose();
					}
					else if (t.IsFaulted)
						Logging.Write($"Error loading background audio: {t.Exception}");
				});

				LoadingTask = null;
				PostLoadSeek = null;
			}
		}

		public override bool LoadAsync(byte[] audio, bool looping, string identifier = null)
		{
			// This method loads and decodes the song asynchronously, but the finalization of the load (creating OpenAL buffers) must be done on the main thread in Update()
			// This means we don't need much synchronization, only ensure to not due multiple loads at the same time.
			StopPendingLoadingSongs();

			if (!base.Load(audio, looping, identifier))
				return false;

			if (CacheTracks.TryGetValue(identifier, out var cachedTrack))
			{
				FinalizeTrackLoad(cachedTrack, true);
				return true;
			}

			// While the song loads silence is better otherwise previews look odd.
			Unload();

			PostLoadSeek = 0;
			LoadingTask = Task.Run(() =>
			{
				return LoadTrackBuffer(audio, looping, identifier);
			});

			return true;
		}

		public override bool Load(byte[] audio, bool looping, string identifier = null)
		{
			StopPendingLoadingSongs();

			if (!base.Load(audio, looping, identifier))
				return false;

			if (CacheTracks.TryGetValue(identifier, out var cachedTrack))
			{
				FinalizeTrackLoad(cachedTrack, true);
				return true;
			}

			try
			{
				var track = LoadTrackBuffer(audio, looping, identifier);
				FinalizeTrackLoad(track, false);
			}
			catch (Exception e)
			{
				Logging.Write($"Error loading background audio: {e}");
				return false;
			}

			return true;
		}

		bool FinalizeTrackLoad(TrackBuffer track, bool fromCache)
		{
			using var _ = new Support.Benchmarker($"Pushing OpenAL buffers for {track.Key}");
			try
			{
				Unload();
				ActiveTrack.Use(track);

				if (ShouldCacheSong(track.Key) && !fromCache)
					CacheTracks[track.Key] = track;

				return true;
			}
			catch (Exception e)
			{
				Logging.Write($"Error loading background audio: {e}");
				Unload();
				return false;
			}
		}

		private static TrackBuffer LoadTrackBuffer(byte[] audio, bool looping, string key)
		{
			string error = null;
			IntPtr decoded = IntPtr.Zero;
			int samples = 0, sampleRate = 0, bitsPerSample = 0, channels = 0;

			key ??= "<no name song>";

			using var _ = new Support.Benchmarker($"Loading audio {key} ({audio.Length} bytes)");

			if ((audio[0] == 'I' && audio[1] == 'D' && audio[2] == '3') || (audio[0] == 0xFF && audio[1] == 0xFB)) // ID3v2 or ID3v1
			{
				Logging.Write($"BackgroundAudioPlayerOpenAL: Detected MP3 file for {key}");
				error = decode_audio_mp3(audio, audio.Length, out decoded, out samples, out sampleRate, out bitsPerSample, out channels);
			}
			else if (audio[0] == 00 && audio[1] == 00 && audio[2] == 00) // Represents the prefix for a NAL unit, just an heuristic for mp4 files
			{
				Logging.Write($"BackgroundAudioPlayerOpenAL: Detected AAC file for {key}");
				error = decode_audio_mp4(audio, audio.Length, out decoded, out samples, out sampleRate, out bitsPerSample, out channels);
			}
			else
			{
				var magic = Convert.ToHexString(audio, 0, 8);
				Logging.Write($"BackgroundAudioPlayerOpenAL: Unknown audio format for {key} with {magic}");
				throw new Exception($"Unknown audio format for {key} with {magic}");
			}

			try
			{
				if (!string.IsNullOrEmpty(error))
					throw new Exception(error);

				if (decoded == IntPtr.Zero || samples <= 0 || sampleRate <= 0 || bitsPerSample != 16 || channels <= 0)
					throw new Exception($"Invalid decoded audio for {key}: {samples} samples, {sampleRate} Hz, {bitsPerSample} bits, {channels} channels");

				return new TrackBuffer(key, sampleRate, channels, samples, looping, 1, 1)
				{
					Body = decoded
				};
			}
			catch
			{
				libc_free(decoded);
				throw;
			}
		}

		public override bool Unload()
		{
			ActiveTrack.Stop();

			var track = ActiveTrack.CurrentTrack;
			ActiveTrack.DetachBuffer();

			if (track != null && !CacheTracks.ContainsKey(track.Key))
				track.Dispose();

			return true;
		}

		public override bool Play()
		{
			if (LoadingTask != null)
			{
				PostLoadSeek = 0;
				return true;
			}

			ActiveTrack.Play();
			return true;
		}

		public override bool Pause()
		{
			if (LoadingTask != null)
			{
				PostLoadSeek = null;
				return true;
			}

			ActiveTrack.Pause();
			return true;
		}

		public override bool Stop(bool reset = true)
		{
			if (LoadingTask != null)
			{
				PostLoadSeek = null;
				return true;
			}

			ActiveTrack.Stop();
			return false;
		}

		// Simulate that the song is playing while it is loading, this is expected by some UI parts.
		public override bool IsElapsing => LoadingTask != null ? true : ActiveTrack.Playing;
		public override double CurrentTime => LoadingTask != null ? 0 : ActiveTrack.CurrentTimeSeconds;

		public override bool SeekTo(int milliseconds) => SeekToInternal(milliseconds, LoadingTask != null);

		bool SeekToInternal(int milliseconds, bool isLoadingTask)
		{
			if (isLoadingTask)
			{
				PostLoadSeek = milliseconds;
				return true;
			}

			if (IsElapsing)
			{
				ActiveTrack.Stop();
				ActiveTrack.CurrentTimeSeconds = milliseconds / 1000f;
				ActiveTrack.Play();
			}
			else
			{
				ActiveTrack.CurrentTimeSeconds = milliseconds / 1000f;
			}

			return base.SeekTo(milliseconds);
		}
	}

	public class BackgroundSourceAL : SourceAL
	{
		internal TrackBuffer CurrentTrack;

		public float DurationSeconds => CurrentTrack.DurationSeconds;

		public float CurrentTimeSeconds
		{
			get
			{
				AL.GetSource(sourceId, ALSourcef.SecOffset, out var offset);
				return offset;
			}
			set
			{
				AL.Source(sourceId, ALSourcef.SecOffset, value);
			}
		}

		public BackgroundSourceAL(int source) : base(source)
		{
			Disposable = false;
		}
		
		internal override void Play()
		{
			// This seems to be called multiple times and can Play() while already playing will reswind to the beginning, breaking pause in-game.
			if (Playing)
				return;

			AL.SourcePlay(sourceId);
		}

		override internal void Stop()
		{
			AL.SourceStop(sourceId);
		}

		internal void Pause()
		{
			AL.SourcePause(sourceId);
		}

		internal override void DeleteBuffer()
		{
			// The lifecycle of LoadedTrack is managed by the bg player ensure this method cannot delete it
			Logging.Write($"Should not be called for {CurrentTrack}.");
			DetachBuffer();
			bufferId = 0;
		}

		internal void Use(TrackBuffer track)
		{
			CurrentTrack = track;
			BufferId = track.GetOrLoadBufferId();
			Looping = track.Looping;
			Pitch = 1;
			Volume = 1;
		}
	}
}