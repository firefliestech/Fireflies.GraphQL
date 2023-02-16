VERSION=$1
ROOT_PATH=$(pwd)

find -name *.nuspec | grep -v "/obj/" | while read -r i
do
	DIR_NAME=$(dirname $i)
	echo "Entering $DIR_NAME"
	cd $DIR_NAME

	dotnet pack -p:PackageVersion=$VERSION --output $ROOT_PATH/nupkgs

	cd $ROOT_PATH
done
