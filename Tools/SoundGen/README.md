# SoundGen

効果音の生成スクリプト。

## セットアップ

```sh
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

mp3 への変換に `ffmpeg` を使うので、こちらも入れておくこと。

```sh
brew install ffmpeg
```

## 実行

```sh
.venv/bin/python generate_move.py  # 移動音 → Assets/Audio/PlayerMove.mp3
```

調整用の定数は各スクリプトの冒頭にまとめてある。どういう音を狙っていて、
どこを動かすと何が壊れるかは、スクリプト先頭の説明文に書いてある。

`.meta` には触らないので、Unity 側の AudioSource の参照は上書きしても切れない。
