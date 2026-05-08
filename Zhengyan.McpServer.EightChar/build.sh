#! /bin/bash

dirname=$(basename "$PWD")

dotnet publish -r "${1:-linux-x64}" -f "${2:-net8.0}" -p:PublishSingleFile=true -o "${3:-../../publish}/$dirname"
