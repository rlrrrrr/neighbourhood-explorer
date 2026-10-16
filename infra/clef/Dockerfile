# llama-server compilé depuis les sources (CPU uniquement, build portable).
# Épinglage : build-arg LLAMA_COMMIT (env CLEF_LLAMA_COMMIT). La référence
# pull/29831/head bouge tant que la PR llama.cpp est ouverte : épingler un
# commit dès qu'une version fonctionne (voir docs/clef.md).
FROM ubuntu:24.04 AS build

ARG LLAMA_COMMIT
RUN apt-get update && apt-get install -y --no-install-recommends \
      ca-certificates curl git build-essential cmake \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /llama.cpp
RUN git init && git remote add origin https://github.com/ggml-org/llama.cpp.git \
    && : "${LLAMA_COMMIT:=pull/29831/head}" \
    && echo "LLAMA_COMMIT=${LLAMA_COMMIT}" \
    && git fetch --depth 1 origin "${LLAMA_COMMIT}" \
    && git checkout --force FETCH_HEAD
RUN cmake -B build -DGGML_NATIVE=OFF -DCMAKE_BUILD_TYPE=Release \
    && cmake --build build --config Release -j"$(nproc)" --target llama-server

FROM ubuntu:24.04 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
      ca-certificates curl libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 clef \
    && mkdir -p /models \
    && chown -R clef:clef /models

COPY --from=build /llama.cpp/build/bin/llama-server /usr/local/bin/llama-server
COPY entrypoint.sh /clef-entrypoint.sh
RUN chmod +x /clef-entrypoint.sh

USER clef
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=15m --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["/clef-entrypoint.sh"]
