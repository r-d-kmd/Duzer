ARG SDK_VERSION=3.1
ARG RUNTIME_VERSION=$SDK_VERSION
ARG RUNTIME_ARGS=""

FROM kmdrd/sdk:$SDK_VERSION AS build

# final stage/image
FROM kmdrd/runtime$RUNTIME_VERSION

ENV APP_NAME app

WORKDIR /app
ONBUILD COPY --from=build /app . 

ENV port 8085
ENV RUNTIME_ARGS=$RUNTIME_ARGS

RUN echo "#!/bin/bash \n dotnet ${APP_NAME}.dll ${RUNTIME_ARGS}" > ./entrypoint.sh
RUN chmod +x ./entrypoint.sh
ENTRYPOINT ["./entrypoint.sh"]