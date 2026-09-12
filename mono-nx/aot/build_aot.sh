#!/bin/sh

set -e

if ! which dotnet >/dev/null 2>&1; then
    # docker
    if [ -e ~/.dotnet/dotnet ]; then
        export DOTNET_ROOT=~/.dotnet
        export PATH=$PATH:$DOTNET_ROOT
    else
        echo "dotnet was not found"
        exit 1
    fi
fi

if [ -d output ]; then
    rm -rf output/
fi

rm romfs/nx-stream/build/*.dll || true

echo Building the project...
dotnet build ../../osu\!stream/nx.csproj -p:Dist=true

echo Trimming the assemblies...

ILLINK=$MONO_NX_ROOT/artifacts/bin/Mono.Linker/Debug/net9.0/illink.dll
ILLINK_CFG=$MONO_NX_ROOT/src/mono/System.Private.CoreLib/src/ILLink/ILLink.Descriptors.xml
ILLINK_CFG1=$MONO_NX_ROOT/src/mono/System.Private.CoreLib/src/ILLink/ILLink.LinkAttributes.xml

LIB_ROOT=$MONO_NX_ROOT/artifacts/bin/mono/libnx.arm64.Debug/
FRAMEWORK_ROOT=$MONO_NX_ROOT/artifacts/bin/runtime/net9.0-libnx-Debug-arm64/

dotnet $ILLINK -x $ILLINK_CFG -x $ILLINK_CFG1 --feature System.Resources.UseSystemResourceKeys true -d $LIB_ROOT -d $FRAMEWORK_ROOT --trim-mode link \
    -d ../../osu\!stream/bin/Debug/net9.0/ \
    -a ../../osu\!stream/bin/Debug/net9.0/nx.dll

echo Mono AOT build...

export MONO_COMPILER=$MONO_NX_ROOT/artifacts/bin/mono/linux.x64.Debug/cross/linux-x64/libnx-arm64/mono-aot-cross

export PATH=$PATH:$DEVKITPRO/devkitA64/bin/

echo "build log" > mono_aot.log

for file in output/*.dll; do
    $MONO_COMPILER --path=output/ --aot=full,static,direct-icalls,direct-pinvoke,nodebug,tool-prefix=aarch64-none-elf-,ntrampolines=20000,ngsharedvt-trampolines=4096,nimt-trampolines=4096 $file >> mono_aot.log
done

# Dlls are needed for metadata
echo copying outputs
cp output/*.dll romfs/nx-stream/build/

grep -r "Linking symbol:" mono_aot.log | sed "s/Linking symbol: '\([^']*\)'\./STATIC_MONO_SYM(\1);/" > source/mono_symbols.h
