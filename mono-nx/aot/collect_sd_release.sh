#!/bin/bash

set -e

if [ ! -e sd_card/osu-stream/icudt77l.dat ]; then
    echo copying full icu data file
    cp $ICU_NX_INSTALL_DIR/share/icu/77.1/icudt77l.dat sd_card/osu-stream/
fi

cp -r ../../osu\!stream/bin/Debug/net9.0/Beatmaps/ sd_card/osu-stream/Beatmaps/
cp -r ../../osu\!stream/bin/Debug/net9.0/Skins/ sd_card/osu-stream/Skins/
cp -r ../../osu\!stream/bin/Debug/net9.0/Localisation/ sd_card/osu-stream/Localisation/

if [ ! -d sd_card/switch ]; then
    mkdir -p sd_card/switch
fi

cp osu-stream-nx.nro sd_card/switch/osu-stream-nx.nro