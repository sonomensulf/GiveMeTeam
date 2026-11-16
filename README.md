# GiveMeTeam
teammate-finding service


## Запуск SQL-скриптов

SQL-скрипты необходимо выполнять в следующем порядке:

1. `db/schema.sql` — создание структуры базы данных: таблиц, индексов, ограничений и триггеров.
2. `db/seed_reference.sql` — заполнение справочников: игры, языки, игровые режимы, уровни опыта и возрастные категории.
3. `db/seed_test.sql` — заполнение тестовыми данными: пользователи, объявления, заявки, сообщения и связи между таблицами.

### Порядок выполнения

Все команды выполняются из корневой папки проекта.

#### 1. Запуск Docker-контейнера с PostgreSQL

```powershell
docker compose -f docker/docker-compose.yml up -d
```

#### 2. Создание базы данных

Создать тестовую базу данных, если она ещё не существует:

```powershell
docker compose -f docker/docker-compose.yml exec db psql -U postgres -c "CREATE DATABASE test_deploy;"
```

#### 3. Копирование SQL-скриптов в контейнер

```powershell
docker compose -f docker/docker-compose.yml cp db/schema.sql db:/tmp/schema.sql
docker compose -f docker/docker-compose.yml cp db/seed_reference.sql db:/tmp/seed_reference.sql
docker compose -f docker/docker-compose.yml cp db/seed_test.sql db:/tmp/seed_test.sql
```

#### 4. Выполнение SQL-скриптов

Скрипты выполняются строго последовательно:

**Создание схемы базы данных:**

```powershell
docker compose -f docker/docker-compose.yml exec db psql -U postgres -d test_deploy -f /tmp/schema.sql
```

**Заполнение справочников:**

```powershell
docker compose -f docker/docker-compose.yml exec db psql -U postgres -d test_deploy -f /tmp/seed_reference.sql
```

**Загрузка тестовых данных:**

```powershell
docker compose -f docker/docker-compose.yml exec db psql -U postgres -d test_deploy -f /tmp/seed_test.sql
```


### Примечания

- Скрипты необходимо выполнять строго в указанном порядке.
- Скрипты `seed_reference.sql` и `seed_test.sql` предназначены для первоначального заполнения пустой базы данных.
- Повторное выполнение скриптов заполнения без предварительной очистки базы данных может привести к ошибкам уникальности записей.
- База данных `test_deploy` используется для проверки корректности развёртывания и загрузки данных.
- Для развёртывания в основной базе данных `givemeteam` необходимо заменить `test_deploy` на `givemeteam` в командах подключения и выполнения SQL-скриптов.
