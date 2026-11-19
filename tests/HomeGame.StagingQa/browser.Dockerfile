FROM mcr.microsoft.com/playwright:v1.64.1-noble
WORKDIR /qa
COPY release-news/package.json release-news/package-lock.json ./
RUN npm ci --ignore-scripts --omit=dev
COPY tests/HomeGame.StagingQa/browser.mjs tests/HomeGame.StagingQa/room-interactions.mjs ./
ENV NODE_ENV=production
ENTRYPOINT ["node", "/qa/browser.mjs"]
