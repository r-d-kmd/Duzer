#!/bin/bash
Black='\033[0;30m'
DarkGray='\033[1;30m'
Red='\033[0;31m'
LightRed='\033[1;31m'
Green='\033[0;32m'
LightGreen='\033[1;32m'
Orange='\033[0;33m'
Yellow='\033[1;33m'
Blue='\033[0;34m'
LightBlue='\033[1;34m'
Purple='\033[0;35m'
LightPurple='\033[1;35m'
Cyan='\033[0;36m'
LightCyan='\033[1;36m'
LightGray='\033[0;37m'
White='\033[1;37m'
NoColor='\033[0m\n'

function log(){
    printf "${LightCyan}$1${NoColor}"
}

if [[ -z "$VERSION" ]]
then
   echo "A packet version is required. '$VERSION' is not a version"
   exit 3
fi

if [[ ! -z $FEED_PAT ]]
then
    FEED_USER=$FEED_PAT
    FEED_PASSWORD=$FEED_PAT
fi

if [[ ! -z $FEED_USER ]]
then
    VSS_NUGET_EXTERNAL_FEED_ENDPOINTS="{\"endpointCredentials\": [{\"endpoint\":\"${FEED_URL}\", \"username\":\"${FEED_USER}\", \"password\":\"${FEED_PASSWORD}\"}]}"
    curl -L https://raw.githubusercontent.com/Microsoft/artifacts-credprovider/master/helpers/installcredprovider.sh  | sh
    echo "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><packageSources><clear /><add key=\"KMD_Package_Feed\" value=\"${FEED_URL}\" /></packageSources></configuration>" >> nuget.config
fi

log "Restore tools"
dotnet tool restore

cd src
BUILD_VERSION=$VERSION
printf "$Green packaging with build version: $VERSION $NoColor"

dotnet pack -c Release
RESULT=$?

if [[ "$RESULT" > 0 ]]
then
    printf "$RED Couldn't pack. Got $RESULT $NoColor"
    exit $RESULT
fi

if [ -z "$API_KEY" ]
then
    API_KEY="az"
fi

if [ -z "$FEED_URL" ]
then
    printf "$Green pushing $VERSION to default endpoint $NoColor"
    dotnet nuget push --api-key $API_KEY "$(ls *.nupkg)"
else
    printf "$Green pushing $VERSION to $FEED_URL $NoColor"
    dotnet nuget push --api-key $API_KEY --source "$FEED_URL" "$(ls *.nupkg)"
fi