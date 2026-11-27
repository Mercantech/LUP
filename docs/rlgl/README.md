# Red Light, Green Light – LUP (test)
Start LUP som beskrevet i repositoryets README. Efter godkendt merge bruger Dokploy den eksisterende deployment; der er ingen ekstra Docker-service eller database. Se /rlgl eller /rlgl/admin. Læreren opretter et rum og kan dele rumkode og tilskuernøgle. Enhedsnøglen er en separat hemmelighed; del den kun med Arduino-operatøren. Ingen af nøglerne placeres i URL eller Git. Rum og resultater er midlertidige (RAM), forsvinder ved deploy/genstart og udløber efter fire timers inaktivitet. Maks. 32 rum, 4 pr. lærer, 32 forskellige enheder pr. rum, 128 sockets samlet. Brug nye rum efter behov; genbrug eksisterende rum via nulstilling. Tilskuere ser nummer, ikke hardware-ID.

## Arduino
Behold den fungerende GOAL-kode og GAP.cpp-ændring. ArduinoHttpClient, ArduinoJson 7, WiFiNINA 2.x, ArduinoBLE 2.x, Arduino_MKRIoTCarrier. Spillerkode i denne mappe. Kopiér secrets.example.h til secrets.h lokalt og udfyld Wi-Fi. Brug aldrig rigtige koder i Git. Upload og send ROOM-linjen fra dashboardet via Serial Monitor (115200, Newline); den lagres kun i RAM. Efter strømafbrydelse skal linjen sendes igen. Ved Wi-Fi-tab skal Arduino genstartes; WSS genforsøges i scanpauser med interval. TLS-certifikat skal være betroet i NINA; brug Arduino IDE certificate uploader ved behov.

## Protokol
WSS /api/rlgl/connect: første tekstbesked {"code":"...","key":"...","deviceId":"782184AA6E9A"}, derefter telemetry {"rssi":-45.2,"rawRssi":-48,"samples":1,"fresh":true,"signalAgeMs":0}. Svaret indeholder light, round, alpha, intervalMs, threshold og place. WSS /api/rlgl/live: første tekstbesked {"code":"...","key":"..."} med separat viewKey; snapshot hvert 500 ms. Ingen browser Origin tilladt på device-endpoint. Lærerens POST endpoints kræver eksisterende JWT og ejerrettighed. Kun friske grønne målinger med -RSSI < threshold giver plads. Rødt lys stopper vinderregistrering, men bevægelseseliminering er ikke implementeret. Skalaen er relativ signalstyrke, ikke meter. En fælles nøgle pr. rum er egnet til demonstration, ikke sikker identitet pr. enhed; klienter med nøglen kan forfalske målinger.

## Test inden merge
dotnet test tests/Mercantec.Tech.Api.Tests --filter FullyQualifiedName~RlglRooms
dotnet build web
Test gæst/elev/lærer, to uafhængige rum, 10 enheder, WSS via nginx, wifi-tab, nulstil, rød/grøn og friske/forældede målinger. Test på fysisk Arduino: WSS/TLS kan blokere scanning; behold den eksisterende UDP-version som fallback. Ingen fysisk test eller build er udført i denne ændring.
