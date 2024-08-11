#!/bin/bash

output_dir="$(pwd)/out/osx-arm64/"
app_name="Stability Matrix.app"

. "./_utils.sh" > /dev/null 2>&1 || . "${BASH_SOURCE%/*}/_utils.sh"

# Parse args
while getopts v: flag
do
    case "${flag}" in
        v) 
          version=${OPTARG}
          ;;
        *) 
          echo "Invalid option: -$OPTARG" >&2
          exit 2
          ;;
    esac
done

shift $((OPTIND - 1))
echo $"Passing extra args to msbuild: $@"

set -e

# Build the app
dotnet \
msbuild \
StabilityMatrix.Avalonia \
-t:BundleApp \
-p:RuntimeIdentifier=osx-arm64 \
-p:UseAppHost=true \
-p:Configuration=Release \
-p:SelfContained=true \
-p:CFBundleName="Stability Matrix" \
-p:CFBundleDisplayName="Stability Matrix" \
-p:CFBundleVersion="$version" \
-p:CFBundleShortVersionString="$version" \
-p:PublishDir="${output_dir:?}/bin" \
"$@"

target_plist_path="${output_dir:?}/bin/${app_name:?}/Contents/Info.plist"

echo "> Checking Info.plist..."
file "${target_plist_path:?}"
plutil -lint "${target_plist_path:?}"

echo "> Copying app to output..."
# Delete existing file
rm -rf "${output_dir:?}/${app_name:?}"
# Copy the app out of bin
cp -r "${output_dir:?}/bin/${app_name:?}" "${output_dir:?}/${app_name:?}"

# Print output location
echo "[App Build Completed]"
print_hyperlink "file:///${output_dir:?}" "${output_dir:?}"
print_hyperlink "file:///${output_dir:?}/${app_name:?}" "${app_name:?}"
echo ""
