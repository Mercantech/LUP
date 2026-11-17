# Hjem.exe på en KVM-vært

VMM-imaget kører en Ubuntu 24.04-gæst med QEMU og KVM. Dockerfilen kontrollerer Canonicals signatur på checksumlisten og validerer Ubuntu-imaget mod den signerede liste. Gæsten bruger sit eget persistente qcow2-drev og kører gVisor inde i Linux-gæsten.

Værten skal give den uprivilegerede QEMU-proces skrive- og læseadgang til `/dev/kvm` via enhedens gruppe. Gæsten har 2 vCPU'er og 4 GiB RAM. Sæt VMM-containerens cgroup-grænse til 2 CPU'er og 5,5 GiB RAM. Brug en tom, dedikeret Docker-volume på `/vm` med automatisk kopiering af volume-markøren fra imaget. VMM-containeren bruger read-only root, `--cap-drop ALL`, `no-new-privileges` og en lille skrivbar tmpfs på `/tmp`. Montér kun KVM-enheden og gæstens eget drev.

På første boot leverer NoCloud-konfigurationen `bootstrap.sh` til `/usr/local/sbin/lup-home-worker-bootstrap` og den offentlige, midlertidige RSA-nøgle til `/etc/lup-home-runner-handoff.pem`. Kør bootstrap som root med `LUP_HOME_WORKER_SLOTS=1` og standardoutput til `/dev/ttyS1`. VMM'en gemmer den konsol i `/vm/bootstrap-console.log` på gæstens private volume. Sæt `LUP_VM_BOOTSTRAP=1` og `LUP_VM_USER_DATA_B64` på bootstrapcontaineren. Brug et separat bridge-net med midlertidig egress til download af verificerede pakker og de tre runtime-images. Bootstrap sender den krypterede engangsoverdragelse og TLS-certifikatets fingeraftryk efter kodeworkertestene.

Bootstrap henter gVisor 20261109.0 fra det officielle udgivelsesarkiv og kontrollerer arkivets SHA-512, før runtime og hjælpeprogrammer installeres i gæsten.

Sluk bootstrapgæsten via QMP, og start VMM-containeren med samme volume på det interne net `lup-home-runner-control`. Brug alias `lup-home-worker`, `unless-stopped` som genstartspolitik og 45 sekunders stopfrist. QEMU bruger begrænset SLIRP og videresender VMM'ens loopback-port 18443 til TLS-proxyen på port 8443 i gæsten. VMM'en lytter på port 8443 på det interne net og videresender TCP-forbindelsen til den lokale port uden at terminere TLS. Fjern bootstrapnettet, når gæsten er flyttet.

Opret det eksterne volume `lup-home-game-connection` før produktionsdeploy. Læg `connection.json` i volumenets rod, ejet af API'ets runtimebruger med filrettighed 0400. Filen indeholder sektionen `HomeGame` med `Enabled`, `RunnerUrl`, `RunnerCertSha256`, `RunnerToken` og `WorkerSlots`. Brug `https://lup-home-worker:8443` som worker-URL og certifikatets verificerede SHA-256-fingeraftryk. API'et er den eneste tjeneste med mountet, som er skrivebeskyttet. API'et læser filen ved opstart, så en rotation kræver genstart af API'et.
