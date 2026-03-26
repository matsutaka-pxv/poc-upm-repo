スクリプト
----------

|ファイル名		|役割										|
|---			|---										|
|serve.js       	|開発用ローカルサーバ								|
|generate-registry.js   |tarballs下の.tgzファイルから docs/-/all および docs/ 下の packument を生成	|
|Scope(s)       	| com.vrmc									|


Unity Editor 側でのレジストリの登録
-----------------------------------

Unity Editor > Edit > Project Settings > Package Manager で以下のレジストリを登録

```
Name            My vrmc registry
URL             https://matsutaka-pxv.github.io/poc-upm-repo
Scope(s)        com.vrmc
```

Unity Editor でのパッケージマネージャの起動
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
