FROM node:22-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends openssl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /qa
COPY tests/HomeGame.StagingQa/issuer.mjs .
ENTRYPOINT ["node", "/qa/issuer.mjs"]
