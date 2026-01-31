#!/bin/bash

rm -rf ./publish/*

version="0.0.1.1"
targets=("osx-arm64" "osx-x64" "win-x86" "win-x64")

for target in "${targets[@]}"; do

    cd InkTagger
    dotnet publish -c Release -r ${target} -o ../publish/${target}
    cd ..

    rm ./publish/${target}/*.pdb
    cp ./LICENSE ./publish/${target}
    cp ./README.md ./publish/${target}
    cp -r ./docs ./publish/${target}

    cd ./publish/${target}
    zip -r "../InkTagger-${target}-${version}".zip .
    cd ../..

done

mkdir ./publish/dll
cp ./InkTaggerLib/bin/Release/net8.0/InkTaggerLib.dll ./publish/dll
cp ./LICENSE ./publish/dll

cd ./publish/dll
zip -r "../InkTaggerLib-${version}.zip" .
cd ../..