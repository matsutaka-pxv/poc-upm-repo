独自UPMレジストリの実験
======================

このリポジトリは https://matsutaka-pxv.github.io/poc-upm-repo という URL で独自の UPM レジストリを提供し、
gitではなくパッケージ(名)としての UniVRM のインストールを可能にするものです。

Unity Editor 側でのレジストリの登録
-----------------------------------

Unity Editor > Edit > Project Settings > Package Manager で以下のレジストリを登録

```
Name            matsutaka-pxv.github.io vrmc registry
URL             https://matsutaka-pxv.github.io/poc-upm-repo
Scope(s)        com.vrmc
```

Unity Editor でのパッケージマネージャの起動とインストール
-------------------------------------------

Unity Editor > パッケージマネージャ > 上部の「Packages:」プルダウンから My Registries を選択


ローカルでの実験方法
====================

以下のコマンドでサーバを起動

```
npm serve.js
```

Unity Editor > Edit > Project Settings > Package Manager で以下のレジストリを登録

|項目		|値				|
|---		|---				|
|Name           | local vrmc registry		|
|URL            | http://localhost:4873		|
|Scope(s)       | com.vrmc			|


原理
====

通常、 UPM はレジストリ URL 直下に以下のファイルがあれば動作します

```
🐈my-reg.com🐈/-/all
🐈my-reg.com🐈/com.vrmc.gltf
🐈my-reg.com🐈/com.vrmc.univrm
🐈my-reg.com🐈/com.vrmc.vrm
```

それぞれは nodejs の Packument というフォーマットの JSON ファイルです。
JSONファイルはバージョンや .tgz の URL およびそのファイルのサイズと SHA1 ハッシュ値を含んでいます。

ただし、 Unity Package Manager は HTTP リクエスト時に Accept-Encoding: gzip を常につけるため、
github.io は UPM の .tgz ファイルをさらに gzip で圧縮してから返します。
この結果、UPM は要求したファイルとは異なるファイルのサイズが異なる旨のエラーを出してインストールを拒否します。

このレジストリでは、これを回避するため、 github の Releases に UPM の .tgz ファイルを配置し、
それぞれの JSON ファイルには Releases 下の .tgz ファイルへのリンクを記述しています。

.tgzファイルの生成方法
--------------------

unity/Editor/PackageTgzExporter.cs を UniVRM プロジェクトににコピーした後、以下のメニューを実行できるようになります

Unity Editor > Menu > UniVRM > Export Package .tgz tarballs

実行後、プロジェクトルートの下に /build/tarballs/ というディレクトリが生成され、その中に .tgz ファイルが配置されます。

