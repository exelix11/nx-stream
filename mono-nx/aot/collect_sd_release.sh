#!/bin/bash

set -e

if [ ! -e sd_card/nx-stream/icudt77l.dat ]; then
    echo copying full icu data file
    cp $ICU_NX_INSTALL_DIR/share/icu/77.1/icudt77l.dat sd_card/nx-stream/
fi

cp -r ../../osu\!stream/bin/Debug/net9.0/Beatmaps/ sd_card/nx-stream/Beatmaps/
cp -r ../../osu\!stream/bin/Debug/net9.0/Skins/ sd_card/nx-stream/Skins/
cp -r ../../osu\!stream/bin/Debug/net9.0/Localisation/ sd_card/nx-stream/Localisation/

if [ ! -d sd_card/switch ]; then
    mkdir -p sd_card/switch
fi

cp nx-stream.nro sd_card/switch/nx-stream.nro