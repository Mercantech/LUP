# Lyd til Bilbo Backend

Bilbos bod har sin egen lydpalet. Tørre fodtrin på træ, papirsedler, resonans fra glas og messinglåg, bløde bærdråber og luft omkring kastene er syntetiseret til de enkelte handlinger. WAV-filerne er mono PCM16 ved 44.100 Hz.

Bilbos varme, underspillede købmandsstemme er dansk Røst Chatterbox med en stemmereference af to lokalt syntetiserede sætninger. Alle fem replikker bruger den samme reference i `source/audio/bilbo-reference.wav`. `audio/speech-provenance.json` registrerer modellen, revisionen, indstillingerne, referencen og hver repliks filhash.

Kør med Python fra den lokale Røst-installation og FFmpeg i PATH. Angiv en ny eksportmappe ved genkørsel.

```text
python -B scripts/audio/build_audio.py --model <lokal Røst-modelmappe> --output <ny lydmappe> --source-output <ny kildemappe>
node scripts/audio/verify_browser.cjs <lydmappe>
python -B scripts/audio/make_demo.py <lydmappe>
```

`audio-validation.json` måler de faktiske filbytes, WAV-format, signalstyrke og længder. `browser-audio-validation.json` kontrollerer dekodning og afspilningsstart efter klik i Chrome. `bilbo-lydproeve.mp3` samler velkomsten, et udsnit af kamplydene og slutreplikken.
