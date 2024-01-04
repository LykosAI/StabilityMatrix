#!/bin/sh

while getopts v: flag
do
    case "${flag}" in
        v) version=${OPTARG};;
        *) echo "Invalid option";;
    esac
done

dotnet \
msbuild \
StabilityMatrix.Avalonia \
-t:BundleApp \
-p:RuntimeIdentifier=osx-arm64 \
-p:UseAppHost=true \
-p:Configuration=Release \
-p:CFBundleShortVersionString="$version" \
-p:SelfContained=true \
-p:CFBundleName="Stability Matrix" \
-p:CFBundleDisplayName="Stability Matrix" \
-p:CFBundleVersion="$version" \
-p:PublishDir="$(pwd)/out/osx-arm64/bin" \

# Copy the app out of bin
cp -r ./out/osx-arm64/bin/Stability\ Matrix.app ./out/osx-arm64/Stability\ Matrix.app
