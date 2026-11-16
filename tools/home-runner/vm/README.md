# Hjem.exe-worker på en eksisterende KVM-vært

`Dockerfile` pakker QEMU og et Ubuntu-image med signeret kontrolsum. `entrypoint.sh` starter en gæst med to vCPU, 4 GiB RAM og en selvstændig qcow2-disk på 32 GiB. Kør VMM'en som bruger 65532 med gruppen til `/dev/kvm`, fjern Linux-capabilities, og giv den en særskilt `/vm`-volume. Begræns VMM-containerens CPU, hukommelse og antal processer på værtens cgroup.

Ved første opstart leverer cloud-init `bootstrap.sh` og den offentlige RSA-nøgle til `/etc/lup-home-runner-handoff.pem`. Gæsten opretter selv tjenestens token, gemmer det i miljøfilen med adgang for root og leverer det som et RSA-OAEP-SHA256-krypteret svar. Administratoren bruger den private modtagernøgle til overførslen til Dokploy.

Start første gang med `LUP_VM_BOOTSTRAP=1` på et separat net med adgang til pakke- og imageregistre. Ved `LUP_HOME_WORKER_READY` har de deployede runner-tests bestået over TLS. Genstart derefter gæsten med samme disk og `LUP_VM_BOOTSTRAP=0`. Tilslut kun VMM'en til det interne `lup-home-runner-control`-net med alias `lup-home-worker`. Bekræft sundhedstjekket over denne forbindelse før Hjem.exe aktiveres.

Gem det udskrevne certifikatfingeraftryk som `HOME_GAME_RUNNER_CERT_SHA256` og brug `https://lup-home-worker:8443` som adresse. Opdatér fingeraftrykket i Dokploy, når certifikatet skiftes. Gem tokenet i Dokploys miljøkonfiguration.
