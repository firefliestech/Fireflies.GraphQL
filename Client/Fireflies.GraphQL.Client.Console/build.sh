VERSION=$1
ROOT_PATH=$(pwd)

dotnet pack -p:PackageVersion=$VERSION --output $ROOT_PATH/nupkgs
