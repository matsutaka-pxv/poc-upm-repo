ローカルサーバの起動
--------------------

以下のコマンドで起動してください

```
cd registry
node serve.js
```


Unity Editor 側でのレジストリの登録
-----------------------------------

Unity Editor > Edit > Project Settings > Package Manager で以下のレジストリを登録

```
Name            My vrmc registry
URL             http://localhost:4873
Scope(s)        com.vrmc
```

Unity Editor でのパッケージマネージャの起動
-------------------------------------------

Unity Editor > パッケージマネージャ > 上部の「Packages:」プルダウンから My Registries を選択
