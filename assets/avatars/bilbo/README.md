# Bilbo Backend · LUP-spilkarakter

Bilbo Backend driver LUPs syltetøjsbod. Hans krøller, grønne fløjlsvest og bare fødder følger det [offentlige portræt](https://lup.dk/characters/bilbo). Originalportræt, modelark og præcise ImageGen-prompter ligger i `source/`.

Åbn `design/bilbo-avatar-v1/index.html` fra den lokale masterpakke for at afspille animationer, skifte genstand eller projektil og høre de danske Chatterbox-replikker. `sheets/` har masterark i 512 px pr. frame. `sheets/256/` har ark til spillet. Hver handling har otte frames i fire kolonner og to rækker.

| Evne | Idé |
| --- | --- |
| Syltetøjskast | Kast et bærglas, der hopper og efterlader en kort klistret plet. |
| Køgreb | Et kort træk og en fartnedsættelse, der kan bringe modstanderen ind i køen. |
| Glasværn | Et skjold, der bruger mindre skjoldkraft mod projektiler. |
| Bodryk | Et kort fremryk. Retning opad kan bruges til redning. |
| Smagepause | Bilbo står stille og genvinder kræfter. En træffer kan afbryde pausen. |
| Ordrekøen | Efter opladning kaster Bilbo tre glas i forskudt takt. |

I LUP låses figuren op efter tre forskellige afleverede backend-videnstjek. Oplåsningen koster intet guld, og figuren følger kontoen. Genforsøg på samme tjek tæller én gang.

Kropsarkene indeholder kun Bilbo. Bærglas, ordreseddel og fælles genstande ligger i `items/`. De flyvende glas ligger i `projectiles/`, og kampens effekter ligger i `effects/`. `attachment-events.json` indeholder ankre og udløsningstidspunkter. Spillet bestemmer genstandens identitet og projektilernes bevægelse.

Stemmefilerne bruger en særskilt syntetisk købmandsreference til lokal dansk Røst/Chatterbox. Modelrevision, tekst og WAV-filernes hashes følger med i `audio/speech-provenance.json`.

Billederne er fremstillet med indbygget ImageGen. Eksportscripts normaliserer transparens, gitter, størrelse og ankre. `validation.json` registrerer de faktiske billedkontroller. Lydrapporter og browserkontroller har deres egne resultater.
