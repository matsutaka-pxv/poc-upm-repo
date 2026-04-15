UniVRM用の独自UPMレジストリ提供実験
===================================

このリポジトリは、 https://matsutaka-pxv.github.io/poc-upm-repo という URL で独自の Unity Package Manager レジストリを提供し、
git URL 指定ではなく、パッケージ名を指定した UniVRM のインストールを可能にします。


パッケージマネージャからの使用例
--------------------------------

最初に、Unity Editor に独自 UPM レジストリを登録します

- Package Manager ( Unity Editor > Menu > Edit > Project Settings > Package Manager ) を開く
- 以下のレジストリを登録
   |項目           |値                                             |
   |--             |--                                             |
   |Name           | matsutaka-pxv.github.io vrmc registry         |
   |URL            | https://matsutaka-pxv.github.io/poc-upm-repo  |
   |Scope(s)       | com.vrmc                                      |

レジストリ登録後、以下の操作でパッケージを追加可能です

- Package Manager ( Unity Editor > Menu > Window > Package Management > Package Manager ) を開く
- Package Manager ウィンドウ左ペイン内の My Registries > matsutaka-pxv.github.io vrmc registry を選択
- 中央ペインでパッケージを選択し、右ペインの「Install」ボタンを押してインストール


他のパッケージからの UniVRM への依存の記述方法
----------------------------------------------

レジストリ登録が済んでいる場合、各パッケージの package.json 内に UniVRM への依存を自然に記述可能です。例 :

```JSON
{
  "description": "My package",
  ...,
  "dependencies": {
    "com.vrmc.vrm": "0.131.0"
  },
}
```


動作の概要
==========

UPM は、レジストリ URL 直下に以下のファイルがあれば動作します

```
🐈my-domain.com/my-reg🐈/-/all
🐈my-domain.com/my-reg🐈/com.vrmc.gltf
🐈my-domain.com/my-reg🐈/com.vrmc.univrm
🐈my-domain.com/my-reg🐈/com.vrmc.vrm
```

各ファイルは nodejs の Packument と呼ばれる JSON ファイルです。
Packument ファイルはバージョン, .tgz の URL, ファイルサイズと SHA1 ハッシュ値を含んでいます。


このリポジトリ内のファイル構成
------------------------------

[docs/](docs/) 下には、独自 UPM レジストリ https://matsutaka-pxv.github.io/poc-upm-repo で公開されるファイルが配置されています

```
docs/-/all		レジストリに含まれる全パッケージのインデクス。JSONファイル
docs/com.vrmc.gltf	パッケージの情報。JSONファイル
docs/com.vrmc.univrm	パッケージの情報。JSONファイル
docs/com.vrmc.vrm	パッケージの情報。JSONファイル
```

各`docs/com.vrmc.*` ファイル内の `tarball` は github.io ではなく、github.com のリポジトリの releases 機能によって公開されている .tgz ファイルへのリンクとなっています。
これは、GitHub Pages で .tgz を公開すると、 UPM が .tgz を正常にダウンロードできない不具合を迂回するためのものです。


.tgz ファイルの公開方法
-----------------------

このレジストリでは、github.com の Releases に UPM の .tgz ファイルを配置し、
それぞれの JSON ファイルには Releases 下の .tgz ファイルへのリンクを記述しています。

以下は Releases 上へのファイル配置の例です :  
[https://github.com/matsutaka-pxv/UniVRM/releases/tag/0.131.0-poc.1/](https://github.com/matsutaka-pxv/UniVRM/releases/tag/0.131.0-poc.1/)


.tgzファイルの生成方法 (1)
--------------------------

本リポトリ内の [unity/Editor/PackageTgzExporter.cs](unity/Editor/PackageTgzExporter.cs) を UniVRM プロジェクトににコピーした後、
以下のメニューを実行できるようになります

Unity Editor > Menu > UniVRM > Export Package .tgz tarballs

実行後、プロジェクトルートの下に /build/tarballs/ というディレクトリが生成され、その中に .tgz ファイルが配置されます。
この .tgz ファイルを GitHub のRelease 機能からリリースし、Packument にその URL を記述するとで、レジストリ経由のインストールが可能になります。


Packument ファイルの生成方法
----------------------------

現在このリポジトリが含む Packument ファイルは手書きのものですが、
dotnet を用いた自動生成ツールを [PackumentGenerator](PackumentGenerator/) に用意しました。

以下のように使用可能です

```
cd PackumentGenerator
git clone https://github.com/vrm-c/UniVRM.git
dotnet run --repo-path=UniVRM --package-dir=UniGLTF --package-name=com.vrmc.gltf --base-url=https://matsutaka-pxv.github.io/poc-upm-repo --packument-dir=docs --tarball-dir=docs/tarballs
tree /a /f docs
```

UniVRM リポジトリ全体から、各パッケージに対する package.json を含むバージョンのみを抽出し、それぞれの .tgz およびそれらをまとめた Packument を出力します。


参照 : 公式UPMが提供しているPackumentファイルの観察
---------------------------------------------------

```
cmd.exe
curl -o com.unity.textmeshpro -LJ https://packages.unity.com/com.unity.textmeshpro
type com.unity.textmeshpro
```


開発用ローカルサーバ - serve.js
-------------------------------

開発用に、簡易的なサーバコードを用意しました。以下のように起動できます

```
node ./serve.js
```

起動後は Package Manager に以下のレジストリ設定を追加することで、
ローカルサーバをレジストリとして使用できます

|項目           |値                     |
|--             |--                     |
|Name           | local registry        |
|URL            | http://localhost:4873 |
|Scope(s)       | com.vrmc              |


TODO
====

- [ ] 何らかの方法での .tgz ファイルへの署名。参照 :
  - [Unity 6.3 LTSのUPM Package signatures対応をする](https://zenn.dev/asus4/scraps/158532dc7768a1)
  - [Package signatures](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-signature.html)
- [ ] CI からパッケージ .tgz アーティファクトを Releases へ追加する
- [ ] /-/all の自動生成
