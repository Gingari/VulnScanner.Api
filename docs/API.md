# HTTP API

[← README](../README.md)

Базовый адрес Docker-примера: `http://localhost:8080`. Локальный профиль `http`: `http://localhost:5096`. Все примеры запросов используют `Content-Type: application/json`. Даты — ISO 8601, сервер формирует время в UTC.

## 1. Создать уязвимость

`POST /v1/vulnerabilities`

```json
{
  "name": "SQL Injection",
  "severity": "Critical"
}
```

- `name`: обязательное непустое название, максимум 200 символов.
- `severity`: обязательное значение `Low`, `Medium`, `High`, `Critical`.
- `id` и `createdAt` задаёт сервер.

Ответ: `201 Created`, заголовок `Location: /v1/vulnerabilities/{id}`. В зависимости от генерации ссылки Location может быть абсолютным URL.

```json
{
  "id": "24524225-3d0e-4b45-8629-d59d6b7b0802"
}
```

## 2. Получить уязвимость

`GET /v1/vulnerabilities/24524225-3d0e-4b45-8629-d59d6b7b0802`

Ответ: `200 OK`.

```json
{
  "id": "24524225-3d0e-4b45-8629-d59d6b7b0802",
  "name": "SQL Injection",
  "severity": "Critical",
  "createdAt": "2026-10-22T12:00:00+00:00"
}
```

Отсутствующий ID: `404`. Ручные записи уязвимостей независимы от находок сканирования: анализатор не вызывает собственный POST API и не переносит Findings в реестр автоматически.

## 3. Запустить анализ

`POST /v1/scans`

```json
{
  "repositoryUrl": "https://github.com/Gingari/VulnScanner.Api"
}
```

Ответ: `202 Accepted`, `Location: /v1/scans/{id}`.

```json
{
  "id": "b6195924-1342-4b75-85ae-4035720b0887"
}
```

Тело поддерживает `repositoryUrl`. Выбор анализаторов, ветки, credentials и команды CLI через API не предусмотрен. Запускаются все зарегистрированные `ICodeScanner`.

URL должен иметь HTTPS-схему, точный разрешённый хост, порт 443 и непустой путь. Логин, пароль, query и fragment запрещены. Расширение `.git` необязательно. Существование репозитория проверяется уже в фоне: несуществующий публичный путь может получить `202`, а затем задачу `Failed`.

Повторный POST создаёт новую независимую задачу. Idempotency key и дедупликация не реализованы.

## 4. Получить состояние и результат

`GET /v1/scans/b6195924-1342-4b75-85ae-4035720b0887`

Пример завершённой задачи с пустым результатом:

```json
{
  "id": "b6195924-1342-4b75-85ae-4035720b0887",
  "repositoryUrl": "https://github.com/Gingari/VulnScanner.Api",
  "status": "Completed",
  "createdAt": "2026-10-22T12:21:19+00:00",
  "lastStatusChangedAt": "2026-10-22T12:21:48+00:00",
  "result": {
    "totalFindings": 0,
    "findings": []
  },
  "error": null,
  "cancellationRequested": false
}
```

`result` до успешного завершения — `null`. При ошибке устанавливаются `Failed` и безопасное сообщение `error`. Получение существующей задачи возвращает `200` даже при `Failed`: HTTP-запрос получения успешен, а фоновая работа — нет.

### Находка

Иллюстративный формат; пример не описывает реальную CVE:

```json
{
  "scanner": "trivy",
  "name": "CVE-TEST-0001: Example.Package",
  "severity": "High",
  "filePath": "packages.lock.json",
  "description": "Test vulnerability. Установлена: 1.0.0. Исправлена: 1.0.1."
}
```

| Поле | Значение |
|---|---|
| `scanner` | Источник: `gitleaks` или `trivy` |
| `name` | Правило Gitleaks или CVE и имя пакета |
| `severity` | `Low`, `Medium`, `High`, `Critical`; для Trivy возможно `Unknown` |
| `filePath` | Файл или target из отчёта инструмента; не обязательно прямая ссылка на исходный файл |
| `description` | Краткое описание без значения найденного секрета |

Дедупликация между анализаторами не выполняется. `totalFindings` — количество объединённых элементов.

### Статусы

| Статус | Значение |
|---|---|
| `Queued` | ID ожидает обработки в очереди |
| `Running` | Выполняется обработка: клонирование, анализ либо ожидание доступа к Trivy |
| `CancellationRequested` | Принят сигнал остановки работающей задачи |
| `Cancelled` | Задача отменена в очереди либо обработчик завершил её после отмены |
| `Completed` | Все анализаторы успешно вернули результат |
| `Failed` | Ошибка Git, анализатора, отчёта или тайм-аут |
| `TimedOut` | Объявлен в модели, но отдельный переход сейчас не реализован |

Основные переходы:

```text
Queued → Running → Completed
Queued → Cancelled
Running → CancellationRequested → Cancelled
Running → Failed
```

`lastStatusChangedAt` — время перехода, не heartbeat и не процент прогресса.

## 5. Отменить задачу

`POST /v1/scans/{id}/cancel`

Тело не требуется. Успех: `200 OK`.

```json
{
  "id": "b6195924-1342-4b75-85ae-4035720b0887",
  "requestedAt": "2026-10-22T12:22:00+00:00",
  "cancellationRequested": true
}
```

- Ожидающая задача сразу помечается `Cancelled`; worker позже пропускает её ID.
- Выполняющаяся задача получает `CancellationRequested`, источник отмены подаёт сигнал. ProcessRunner пытается остановить дерево внешнего процесса. После обработки отмены устанавливается `Cancelled`.
- `true` подтверждает **принятие запроса**, а не гарантированную синхронную остановку процесса до отправки HTTP-ответа. Подтверждение завершения — последующий GET со статусом `Cancelled`.
- Повторный запрос при `CancellationRequested` возвращает `200` и время первого запроса. После конечного состояния, включая `Cancelled`, ответ — `409`.
- Неизвестный ID — `404`.
- Гонка завершения и отмены разрешается блокировкой: если первым зафиксирован `Completed`, отмена получит `409`; если первой принята отмена, завершение не должно записать `Completed`.

**Нюанс текущей модели:** в ответе GET присутствует отдельное поле `cancellationRequested`, но текущий код не синхронизирует его с переходами статуса. Не используйте это поле GET как источник истины. Для наблюдения используйте `status`; подтверждение принятия запроса берите из ответа POST отмены. Поле рекомендуется удалить из модели задачи либо согласовать с логикой перед дальнейшим развитием API.

## 6. Ошибки

| HTTP | Ситуация |
|---|---|
| `400` | Невалидный JSON/контракт, критичность, название или URL; запрещённый хост |
| `404` | Неизвестный ID либо маршрут |
| `409` | Отмена задачи в конечном состоянии |
| `429` | Нет свободного места в очереди |
| `500` | Непредвиденная ошибка HTTP-обработки |

Исключения сервиса форматируются глобальным обработчиком:

```json
{
  "title": "Конфликт состояния",
  "status": 409,
  "detail": "Завершённое сканирование нельзя отменить.",
  "instance": "/v1/scans/b6195924-1342-4b75-85ae-4035720b0887/cancel",
  "traceId": "example-trace-id"
}
```

Ошибки валидации `[ApiController]` могут дополнительно содержать `errors` по полям. Не привязывайтесь к точному тексту сообщения или наличию всех необязательных полей Problem Details.

Маршруты используют `{id:guid}`. Синтаксически неправильный GUID не соответствует маршруту и обычно даёт `404`, а не `400`.

Внутренний тайм-аут анализа не превращает GET в `504`: задача переходит в `Failed`, GET возвращает `200` с её состоянием.

## 7. Полный пример в PowerShell

```powershell
$base = "http://localhost:8080"

$vuln = Invoke-RestMethod -Method Post `
    -Uri "$base/v1/vulnerabilities" `
    -ContentType "application/json" `
    -Body (@{ name = "Test vulnerability"; severity = "High" } | ConvertTo-Json)

Invoke-RestMethod -Uri "$base/v1/vulnerabilities/$($vuln.id)"

$scan = Invoke-RestMethod -Method Post `
    -Uri "$base/v1/scans" `
    -ContentType "application/json" `
    -Body (@{
        repositoryUrl = "https://github.com/Gingari/VulnScanner.Api"
    } | ConvertTo-Json)

Invoke-RestMethod -Uri "$base/v1/scans/$($scan.id)"

# Чтобы запросить отмену ещё не завершённой задачи:
Invoke-RestMethod -Method Post -Uri "$base/v1/scans/$($scan.id)/cancel"
```
