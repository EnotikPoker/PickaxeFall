# Подготовка Google Play

Проверено по официальным источникам 18.11.2026. Это инструкция для владельца аккаунтов; регистрация, публикация, платежи и размещение страниц автоматически не выполнялись.

## Аккаунты и подключение

1. Создать Play Console и платёжный профиль с реальными белорусскими данными, пройти требуемую проверку личности/организации, адреса и банка. Доступность конкретного способа выплат проверяется в аккаунте; код игры не может её обеспечить.
2. Создать приложение `com.enotikpoker.pickaxefall`, подключить Play App Signing **со своим существующим app-ключом** из `Builds/Private/googleplay/app.keystore`. Использовать официальный PEPK-процесс консоли. Upload-ключ — отдельный `upload.keystore`. Не выбирать случайно другой app-ключ: скачиваемая Play APK должна иметь ту же app-подпись. Хранить оба ключа и `signing.json` вне ПК в защищённой резервной копии. Никогда не прикладывать к PR/issue.
3. Play Games Services v2: включить Saved Games, создать Android OAuth credential для package ID + SHA-1 **app signing certificate**. Для локальных Play APK app-ключ тот же; при необходимости добавить отдельные тестовые credentials. Добавить тестеров и 12 стандартных одноразовых достижений из `GooglePlay/Achievements.csv`. Взять application ID и achievement IDs; включить доступ к конфигурации для тестеров. Для 1.0 использовать новый набор достижений вместе со сменой SaveEpoch.
4. Google Play Billing: один non-consumable продукт `remove_ads`, ориентир 2.99 USD с локальными ценами/налогами. Активировать, добавить license testers, взять Base64 licensing public key. Никаких consumable пакетов монет в 0.8.4 нет. Покупки проверять через установку из тестового трека, а не только скачанной APK.
5. В РСЯ зарегистрировать нативное Android-приложение и два формата (interstitial/rewarded), указать фактическую аудиторию, рейтинг и категории. Эти блоки обслуживают только взрослых. На тесте используются официальные demo IDs; реальные ID — только после проверки аккаунта.
6. Kidoz: получить собственные publisher ID/token, **сначала подтвердить поддержку издателя/банка из Беларуси и выплаты**. Проверить конкретную версию 10.1.9 в SDK Index. У провайдера подтвердить детские форматы с закрытием не позднее пяти секунд, включая rewarded. Проверить на устройстве фактические креативы и маркировку. Только затем выставлять `kidozAccountApproved` и `kidozCloseWithinFiveSecondsConfirmed`. Эти флаги — запись ручного подтверждения, не автоматическая сертификация.
7. Заполнить приватную копию `Assets/PickaxeFall/Resources/GooglePlay/config.json`, например `Builds/Private/googleplay/release.json`. Имя/почта пока намеренно отсутствуют по решению владельца. `testMode=false` — только для публикации. Поля издателя SDK являются клиентской конфигурацией; закрытые серверные ключи сюда не помещать.

## Политика и карточка

Разместить HTML-политику конфиденциальности и страницу удаления по публичным HTTPS URL без авторизации. Купить сервер не требуется: подходит бесплатный статический хостинг. Шаблоны находятся в `GooglePlay/`; генератор ниже требует реальные контакты и ссылки. `app-ads.txt` взять из кабинетов рекламных сетей и опубликовать в корне домена сайта разработчика, указанного в карточке. Не придумывать seller ID. Добавить ссылки в конфигурацию игры и Play Console.

`DataSafety.md` — рабочая таблица, а не автоматически заполненная анкета. Сверить фактические сбор и передачу данных каждого SDK с его актуальным раскрытием и настройками кабинета. Не отмечать «данные не собираются»: реклама и Play Games передают данные. Возрастное число не сохраняется и не отправляется нашим кодом; локально хранится только группа. Запросов разрешений местоположения/контактов/файлов нет. Выбор без персонализации не означает отсутствия обработки IP и технических данных.

Аудитория — смешанная; её возрастные категории и IARC описывают настоящую игру. Не завышать рейтинг, чтобы обойти детские требования. Игровая рулетка бесплатна, без денежных ставок, вывода и покупки попыток. Отметить рекламу и встроенные покупки. Внешняя ссылка Telegram скрыта в Play-сборке, атрибуция музыки сохранена внутри приложения. При создании карточки не использовать название/логотипы Minecraft или чужие скриншоты. Тексты — `GooglePlay/StoreListing.md`; иконка 512×512, feature graphic 1024×500 и настоящие скриншоты должны быть подготовлены/проверены отдельно в консоли.

```bash
python3 Tools/PreparePlayPages.py --draft --output Builds/PlayPagesDraft
# После заполнения контактов, HTTPS URL и app-ads.txt из кабинетов:
python3 Tools/PreparePlayPages.py --config Builds/Private/googleplay/release.json --app-ads Builds/Private/googleplay/app-ads.txt --output Builds/PlayPages
```

## Сборка

Unity 6000.4.0f1, Android SDK/API/build-tools 36, ARM64 IL2CPP. Зависимости фиксированы в `Tools/GooglePlayDependencies.json`. Unity IAP добавляется только в изолированную копию, Android SDK bridge также вносится временно. Рабочий проект и обычная APK не получают эти SDK. Yandex 7.18.8 выбран для совместимости с AGP 8.10 Unity; перед публикацией проверить поддержку версии в SDK Index/кабинете. Unity IAP 5.3.0 использует Billing 8.3.0.

```bash
python3 Tools/SyncValidationProject.py
python3 Tools/BuildGooglePlay.py test-aab --config Builds/Private/googleplay/release.json
python3 Tools/BuildGooglePlay.py test-apk --config Builds/Private/googleplay/release.json
python3 Tools/BuildGooglePlay.py preflight --config Builds/Private/googleplay/release.json
python3 Tools/BuildGooglePlay.py production-aab --config Builds/Private/googleplay/release.json
```

`test-*` принудительно включает testMode, AAB подписывается upload-ключом, APK — app-ключом. Запуск без `--config` создаёт сборку с безопасным пустым шаблоном: играется офлайн, реальные облако/покупки и детские объявления не подключены. Публичный preflight обязан её отклонять. Реальные транзакции использовать только с license tester; testMode сам по себе не подменяет биллинг.

Обычный APK по-прежнему собирается `Tools/BuildPlayers.py android`, устанавливается обновлением прежней debug APK и не требует Google. Не распространять Play APK под видом обновления старой debug APK: подписи разные. По согласованию переноса тестового прогресса нет; 0.8.4 сама его не удаляет.

## Проверка на аккаунтах перед выпуском

- Internal testing: установка из Play, гостевой запуск офлайн, RU/EN, сохранённый спуск, возврат из фона. Никаких обязательных входа/оплаты/рекламы для старта.
- Возраст: пустое поле, неверный ввод, пропуск, ребёнок, взрослый; до выбора нет сети рекламы, при unknown/child нет Yandex/AppMetrica. Проверить трафик, фактические Android providers и отсутствие AD_ID/location. Возрастные настройки применяются после полного перезапуска, до него показы выключены.
- Реклама: обе сети, нет fill/ошибка/поворот/фон, отказ от rewarded, закрытие ≤5 секунд в детских форматах, один reward, сохранение награды после перезапуска, 120 активных секунд, нет mid-run. Для семьи рекламное вознаграждение не требует просмотра дольше допустимого закрытия; условие/выдачу подтвердить с провайдером.
- Billing: тестовая покупка, отмена, pending/parent approval, process death до подтверждения, restore на чистой установке, offline cache, возврат/отзыв при успешном FetchPurchases, отсутствие искусственной выдачи из облака. Локальная валидация постоянного права имеет обычные ограничения клиента; серверный магазин в план не входил.
- Play Games: взрослый и допустимый supervised account, отказ входа, два устройства с конфликтом, переключение аккаунта, потеря сети и повтор, удаление облака, 12 достижений. Проверить отсутствие бесконечных загрузок и смешивания валют. Play Games может не быть доступен у конкретного детского аккаунта; локальная игра остаётся.
- Pre-launch report, Android vitals, 16 KB devices, permissions, install/update с app-подписью; реальные мобильные экраны, отсутствие Dev/QA и ключей.
- Для новых личных аккаунтов после 13.11.2023: закрытый тест минимум с 12 тестерами, непрерывно подключёнными не менее 14 дней, затем запрос production access. Решение принимает Google; сборка AAB не заменяет эти этапы.

## Источники

- [Families policy](https://support.google.com/googleplay/android-developer/answer/9893335) — смешанная аудитория, возрастной экран и пять секунд.
- [Список Families SDK](https://support.google.com/googleplay/android-developer/answer/12955712) — Kidoz; проверять каждую версию в SDK Index.
- [Новые личные аккаунты](https://support.google.com/googleplay/android-developer/answer/14151465) — закрытый тест.
- [Google Saved Games](https://developer.android.com/games/pgs/android/saved-games) — снимки и ручной конфликт.
- [Unity IAP setup](https://docs.unity.com/en-us/iap/set-up-in-app-purchasing), [receipt validation](https://docs.unity.com/en-us/iap/receipt-validation).
- [Yandex Android initialization](https://ads.yandex.com/helpcenter/en/dev/android/quick-start) — ручной запуск и privacy до SDK.
- [Kidoz SDK](https://github.com/Kidoz-SDK/kidoz-mobile-sdk).
