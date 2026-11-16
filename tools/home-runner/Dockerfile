FROM python:3.13-slim-bookworm AS python
COPY sandbox.py home.py /opt/home-sdk/
ENV PYTHONDONTWRITEBYTECODE=1 HOME=/work TMPDIR=/tmp
WORKDIR /work
USER 65532:65532
ENTRYPOINT ["timeout", "--foreground", "--signal=KILL", "905", "python3", "/opt/home-sdk/sandbox.py", "python"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS csharp
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/*
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 DOTNET_EnableDiagnostics=0 DOTNET_CLI_HOME=/work HOME=/work NUGET_PACKAGES=/work/.nuget \
    PYTHONDONTWRITEBYTECODE=1 TMPDIR=/tmp
COPY sandbox.py /opt/home-sdk/
COPY csharp/ /opt/home-sdk/csharp/
RUN dotnet build /opt/home-sdk/csharp/sdk/HomeRunner.csproj -c Release -o /opt/home-sdk/bootstrap \
    --configfile /opt/home-sdk/csharp/NuGet.Config
WORKDIR /work
USER 65532:65532
ENTRYPOINT ["timeout", "--foreground", "--signal=KILL", "905", "python3", "/opt/home-sdk/sandbox.py", "csharp"]

FROM python:3.13-slim-bookworm AS arduino
RUN apt-get update && apt-get install -y --no-install-recommends g++ nlohmann-json3-dev && rm -rf /var/lib/apt/lists/*
COPY sandbox.py /opt/home-sdk/
COPY arduino/ /opt/home-sdk/arduino/
RUN g++ -std=c++17 -O1 -c /opt/home-sdk/arduino/VirtualHome.cpp -o /opt/home-sdk/VirtualHome.o
ENV PYTHONDONTWRITEBYTECODE=1 HOME=/work TMPDIR=/tmp
WORKDIR /work
USER 65532:65532
ENTRYPOINT ["timeout", "--foreground", "--signal=KILL", "905", "python3", "/opt/home-sdk/sandbox.py", "arduino"]
