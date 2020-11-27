#!/bin/bash
if [[ -z "$VERSION" ]]
then
   echo "A packet version is required"
   exit 1
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

dotnet build --configuration Release
dotnet paket pack --version "$VERSION" . 

if [ -z $API_KEY ]
then
    $API_KEY="az"
fi

if [ -z $FEED_URL ]
then
    dotnet nuget push --api-key $API_KEY "$(ls *.nupkg)"
else
    dotnet nuget push --api-key $API_KEY --source "$FEED_URL" "$(ls *.nupkg)"
fi