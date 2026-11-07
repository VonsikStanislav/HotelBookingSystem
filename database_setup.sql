-- Выполнить в той же базе PostgreSQL, где уже созданы Hotels, Rooms и Bookings.
-- Дополнительные таблицы, необходимые текущему C#-проекту.

CREATE TABLE IF NOT EXISTS "Guests" (
    "Id" INTEGER PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Phone" VARCHAR(30) NOT NULL DEFAULT '',
    "Email" VARCHAR(120) NOT NULL DEFAULT ''
);

INSERT INTO "Guests" ("Id", "Name", "Phone", "Email") VALUES
    (1, 'Иван Петров', '+375291112233', 'ivan@example.com'),
    (2, 'Анна Смирнова', '+375292223344', 'anna@example.com')
ON CONFLICT ("Id") DO NOTHING;

-- Текущий клиент умеет оплачивать бронирование, поэтому сохраняем платежи в БД.
CREATE TABLE IF NOT EXISTS "Payments" (
    "Id" SERIAL PRIMARY KEY,
    "BookingId" INTEGER NOT NULL REFERENCES "Bookings"("Id"),
    "Amount" NUMERIC(10, 2) NOT NULL CHECK ("Amount" >= 0),
    "Status" VARCHAR(20) NOT NULL,
    "PaidAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Разрешаем статус Paid, который используется операцией оплаты.
ALTER TABLE "Bookings" DROP CONSTRAINT IF EXISTS "Bookings_Status_check";
ALTER TABLE "Bookings" ADD CONSTRAINT "Bookings_Status_check"
    CHECK ("Status" IN ('Confirmed', 'Cancelled', 'Paid'));

-- Если Guests ранее не содержала записей, синхронизируем sequence не требуется:
-- Id здесь задан вручную как INTEGER, без SERIAL.
