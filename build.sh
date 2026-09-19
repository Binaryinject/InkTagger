#!/bin/bash

rm -rf ./publish/*

version="0.2.0"
targets=("osx-arm64" "osx-x64" "linux-x64" "win-x86" "win-x64")

for target in "${targets[@]}"; do

    cd InkTagger
    dotnet publish -c Release -r ${target} -o ../publish/${target}
    cd ..

    rm -f ./publish/${target}/*.pdb
    cp ./LICENSE ./publish/${target}
    cp ./README.md ./publish/${target}
    cp -r ./docs ./publish/${target}

    cd ./publish/${target}
    zip -r "../InkTagger-${target}-${version}".zip .
    cd ../..

done

mkdir -p ./publish/dll
cp ./InkTaggerLib/bin/Release/net10.0/InkTaggerLib.dll ./publish/dll
cp ./LICENSE ./publish/dll

cd ./publish/dll
zip -r "../InkTaggerLib-${version}.zip" .
cd ../..
