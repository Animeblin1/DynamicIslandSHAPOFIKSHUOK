# Шрифты

Остров рисуется шрифтом **SF Pro**, но сами файлы в репозиторий не входят: лицензия Apple запрещает их распространять.

Без них всё работает — текст берёт `Segoe UI Variable` (или `Segoe UI`), который есть в Windows.

Чтобы получить оригинальный вид, скачайте SF Pro с [developer.apple.com/fonts](https://developer.apple.com/fonts/) и положите в эту папку:

- `SF-Pro-Display-Semibold.otf`
- `SF-Pro-Text-Regular.otf`
- `SF-Pro-Text-Medium.otf`
- `SF-Pro-Text-Semibold.otf`

При сборке все `*.otf` и `*.ttf` отсюда встраиваются в exe.
