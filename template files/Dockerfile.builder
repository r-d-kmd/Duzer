
FROM kmdrd/sdk AS build-base

ARG FEED_PAT_ARG
ARG FEED_USER_AEG
ARG FEED_PASSWORD_ARG

ENV FEED_PAT ${FEED_PAT_ARG}
ENV FEED_USER ${FEED_USER_ARG}
ENV FEED_PASSWORD ${FEED_PASSWORD_ARG}

COPY setEnv.sh /tmp/setEnv.sh
RUN chmod +x /tmp/setEnv.sh
RUN ./tmp/setenv.sh

COPY Paket.Restore.targets /.paket/Paket.Restore.targets
COPY paket.lock .
COPY paket.dependencies .

RUN dotnet paket restore

FROM build-base

ONBUILD COPY /src/ /source
WORKDIR /source

ONBUILD RUN export PROJECT_NAME=(ls *.?sproj)
ONBUILD RUN export EXECUTABLE="$(expr "$PROJECT_NAME" : '\(.*\)\..sproj').dll"

ONBUILD RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app -p:PublishReadyToRun=true