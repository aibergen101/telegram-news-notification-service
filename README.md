# TelegramApi

HTTP API для публикации новостей и опросов в Telegram-каналы. .NET 10, PostgreSQL, Wolverine (CQRS), EF Core, Telegram.Bot.

## Структура

| Проект | Назначение |
|---|---|
| `TelegramApi` | HTTP-слой: эндпоинты, контракты, обработка ошибок, конфигурация |
| `TelegramApi.Application` | Команды, запросы, handlers, валидация, интерфейсы |
| `TelegramApi.Domain` | Сущности и enum |
| `TelegramApi.Infrastructure` | EF Core, миграции, Telegram.Bot, health check PostgreSQL |

## Запуск локально

1. Поднять PostgreSQL и создать пустую базу `telegramapi`.
2. Задать токен бота и (при необходимости) строку подключения через user-secrets:

   ```bash
   dotnet user-secrets set "TelegramBot:BotToken" "<токен от @BotFather>" --project TelegramApi
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=telegramapi;Username=postgres;Password=<пароль>" --project TelegramApi
   ```
3. Применить миграции и запустить:

   ```bash
   dotnet ef database update --project TelegramApi.Infrastructure --startup-project TelegramApi
   dotnet run --project TelegramApi
   ```
4. Документация и выполнение запросов: Scalar на `/scalar/v1`, OpenAPI-документ на `/openapi/v1.json`.

Бот должен быть администратором канала с правом «Публикация сообщений».

## Запуск в Docker

```bash
docker build -t telegramapi .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=telegramapi;Username=<user>;Password=<password>" \
  -e TelegramBot__BotToken="<токен>" \
  telegramapi
```

Токен и пароль в образ не попадают: они передаются только при запуске.

## Основные эндпоинты

| Метод и путь | Что делает |
|---|---|
| `POST /api/publications` | Текстовая публикация (JSON) |
| `POST /api/media-publications` | Публикация с файлами (multipart/form-data) |
| `GET /api/publications/{id}` | Публикация по идентификатору |
| `PUT /api/publications/{id}` | Редактирование текста (подписи), формата и кнопок отправленной публикации |
| `DELETE /api/publications/{id}` | Удаление публикации (все сообщения альбома) |
| `POST /api/polls` | Создание опроса |
| `GET /api/polls/{id}` | Опрос по идентификатору |
| `POST /api/polls/{id}/close` | Закрытие опроса |
| `DELETE /api/polls/{id}` | Удаление опроса |
| `PUT /api/publications/{id}/pin`, `PUT /api/polls/{id}/pin` | Закрепить сообщение (`?disableNotification=true` — без уведомления) |
| `DELETE /api/publications/{id}/pin`, `DELETE /api/polls/{id}/pin` | Открепить сообщение |
| `GET /api/publications-history` | Единая история: фильтры `status`, `channelAlias`, `from`, `to`, пагинация `page`, `pageSize` |
| `GET /api/bot/check` | Проверка токена бота |
| `GET /api/channels/{alias}` | Информация о канале, alias вместе с `@`, например `/api/channels/@my_channel` |
| `GET /health` | Health check: приложение и PostgreSQL (Telegram проверяется отдельно через `GET /api/bot/check`) |

## Решения и ограничения

**Коды ошибок (Problem Details).**

| Ситуация | Код |
|---|---|
| Ошибка валидации | 400 |
| Публикация, опрос или канал не найдены | 404 |
| У бота нет права публиковать | 403 |
| Операция невозможна в текущем статусе (закрытие, редактирование, закрепление) или `Idempotency-Key` повторён с другим содержимым | 409 |
| Telegram отклонил содержимое (например, неверная разметка) | 422 |
| Telegram вернул ошибку или недоступен | 502 |
| Telegram не ответил за `TelegramBot:TimeoutSeconds` | 504 |

Если Telegram отказал после создания записи, она остаётся в истории со статусом `Failed`.

**Жизненный цикл.** Запись создаётся до отправки со статусом `Failed`, после подтверждения Telegram получает `Sent`, после удаления сообщений в Telegram `Deleted`. Автоматических повторов нет, `Telegram.Bot` настроен без retry.

**Файлы.** Фото отправляются только JPEG и PNG, остальные разрешённые типы уходят документом. Нельзя смешивать изображения и документы в одной публикации: Telegram не принимает такие альбомы. Кнопки допустимы только для одного файла, потому что к альбому Telegram их не прикрепляет. Лимиты: фото до 10 МБ, документ до 50 МБ, в альбоме до 10 файлов, подпись до 1024 символов.

**Канал.** Alias канала передаётся вместе с `@`, например `@my_channel`.

**Ссылки.** Ссылку можно передать в тексте или отдельным полем `link` (абсолютный http(s) адрес). Ссылка из `link` добавляется отдельной строкой в конец текста или подписи в синтаксисе выбранного формата; превью ссылок не показывается. Текстовой публикации нужен `text` или `link`.

**Кнопки.** Только абсолютные ссылки по `https`, до 100 штук, каждая в своей строке. В JSON-запросах это массив `buttons`; в multipart-форме публикации с файлом — одно поле `buttons` с JSON-массивом, например `[{"text":"Open","url":"https://example.com"}]`. Некорректный JSON возвращает 400.

**Опросы.** Продолжительность в секундах от 5 до 600 (ограничение `open_period` Telegram). Закрытие повторно безопасно.

**Удаление.** Повторное удаление безопасно. Запись получает `Deleted` только после успешного ответа Telegram.

**Редактирование.** Только для `Sent`. Текст, формат и кнопки заменяются целиком (пустой список кнопок убирает их), файлы не меняются. У альбома меняется подпись первого сообщения, кнопки недоступны. Опросы Telegram редактировать не позволяет.

**Закрепление.** Только для `Sent`, у альбома закрепляется первое сообщение. Повторные вызовы безопасны. Боту нужно право администратора на закрепление (в канале это право редактирования сообщений), иначе 403.

**Идемпотентность создания.** Заголовок `Idempotency-Key` (до 255 видимых ASCII-символов) для `POST /api/publications`, `/api/media-publications`, `/api/polls`. Ключ хранится в записи бессрочно, уникален в БД. Повтор того же запроса возвращает существующую запись с кодом 200 без повторной отправки в Telegram. Тот же ключ с другим содержимым (включая содержимое файлов) возвращает 409.
