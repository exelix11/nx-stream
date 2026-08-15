//TODO: change this class to just be a static. singleton is pointless.

using System;
using System.IO;

namespace osum.AssetManager
{
    /// <summary>
    /// AssetManagers abstract the file IO to manage assets in a multi-platform environment.
    /// Assets are skins, hitsounds, textures that come with the game.
    /// These are, depending on the platform, located in the executable itself.
    /// Maps are not included as assets to prevent oversized executables.
    /// This base implementation of this class uses normal file IO.
    /// </summary>
    public class NativeAssetManager
    {
        internal static NativeAssetManager Instance { get; private set; }
        readonly string Root;

        public NativeAssetManager(string root = null)
        {
            //if (Instance != null)
            //    throw new Exception("singleton");

            Root = root;
            Instance = this;

            Logging.Write($"NativeAssetManager: initialized with root {Root}");
        }

        protected virtual string GetPath(string filename)
        {
            if (Root != null)
                filename = Path.GetFullPath(Path.Combine(Root, filename));

            if (!File.Exists(filename))
                Logging.Write($"NativeAssetManager: resolved {filename} does not exist");
            
            return filename;
        }

        internal virtual bool FileExists(string filename)
        {
            return File.Exists(GetPath(filename));
        }

        internal virtual Stream GetFileStream(string filename)
        {
            return File.OpenRead(GetPath(filename));
        }

        internal virtual byte[] GetFileBytes(string filename)
        {
            return File.ReadAllBytes(GetPath(filename));
        }
    }
}