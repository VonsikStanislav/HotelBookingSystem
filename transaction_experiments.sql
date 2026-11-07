-- Эксперименты для части 3. Выполняйте ПО ОДНОМУ блоку.
-- Важно: каждый запуск используйте уникальное имя гостя или удаляйте тестовые строки.

-- ПОДГОТОВКА: убедитесь, что номер 101 существует и AvailablePlaces > 0.
-- Узнать ID номера:
SELECT "Id", "RoomNumber", "AvailablePlaces"
FROM "Rooms" WHERE "RoomNumber" = '101';

/* ЭКСПЕРИМЕНТ 1 — БЕЗ ТРАНЗАКЦИИ
   Выполнить INSERT отдельно, затем искусственную ошибку отдельно.
   INSERT фиксируется автоматически (autocommit), ошибка не отменяет его. */
INSERT INTO "Bookings" ("RoomId", "GuestName", "CheckIn", "CheckOut", "TotalPrice", "Status")
SELECT "Id", 'NoTransaction Test', CURRENT_DATE + 10, CURRENT_DATE + 12, 240.00, 'Confirmed'
FROM "Rooms" WHERE "RoomNumber" = '101'
LIMIT 1;

-- Запустите следующую строку ОТДЕЛЬНО, чтобы получить ошибку:
-- SELECT 1 / 0;
-- Затем проверьте: бронирование есть, AvailablePlaces не изменилось.
SELECT * FROM "Bookings" WHERE "GuestName" = 'NoTransaction Test';
SELECT "RoomNumber", "AvailablePlaces" FROM "Rooms" WHERE "RoomNumber" = '101';

/* ЭКСПЕРИМЕНТ 2 — ТРАНЗАКЦИЯ С COMMIT
   Повторное использование номера возможно только если AvailablePlaces > 0. */
BEGIN;
WITH target_room AS (
  SELECT "Id", "PricePerNight" FROM "Rooms"
  WHERE "RoomNumber" = '101' AND "AvailablePlaces" > 0
  LIMIT 1
), new_booking AS (
  INSERT INTO "Bookings" ("RoomId", "GuestName", "CheckIn", "CheckOut", "TotalPrice", "Status")
  SELECT "Id", 'Commit Test', CURRENT_DATE + 20, CURRENT_DATE + 22, "PricePerNight" * 2, 'Confirmed'
  FROM target_room RETURNING "RoomId"
)
UPDATE "Rooms" SET "AvailablePlaces" = "AvailablePlaces" - 1
WHERE "Id" IN (SELECT "RoomId" FROM new_booking);
COMMIT;
-- Проверка:
SELECT * FROM "Bookings" WHERE "GuestName" = 'Commit Test';
SELECT "RoomNumber", "AvailablePlaces" FROM "Rooms" WHERE "RoomNumber" = '101';

/* ЭКСПЕРИМЕНТ 3 — ТРАНЗАКЦИЯ С ROLLBACK
   Ошибка здесь имитируется вручную: после INSERT выполняется ROLLBACK. */
BEGIN;
INSERT INTO "Bookings" ("RoomId", "GuestName", "CheckIn", "CheckOut", "TotalPrice", "Status")
SELECT "Id", 'Rollback Test', CURRENT_DATE + 30, CURRENT_DATE + 32, 240.00, 'Confirmed'
FROM "Rooms" WHERE "RoomNumber" = '101' LIMIT 1;
-- Вместо второго изменения намеренно отменяем всю транзакцию:
ROLLBACK;
-- Проверка: строки Rollback Test нет.
SELECT * FROM "Bookings" WHERE "GuestName" = 'Rollback Test';
