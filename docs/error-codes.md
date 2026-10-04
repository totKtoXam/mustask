# API Error Code Reference

**This file is generated** from `contracts/errors/*.json` by the
`src/backend/tests/Mustask.Contracts.Tests` test (`ErrorCodesReferenceTests`) —
do not edit it by hand, the edit will be lost on the next regeneration, and a
mismatch with the catalog fails the build.

Regenerate after editing the catalog:

```sh
MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests
```

The "kk" column is the entry's Kazakh text; the "needs review" mark means
the text was written by a non-native speaker and the auto-check only
guarantees it exists and differs from the Russian, not its quality
(`contracts/README.md`).

| Code | HTTP status(es) | Russian | Kazakh | Origin | Deprecated | kk |
|---|---|---|---|---|---|---|
|dictionaries.invalid_page_size|400|Некорректные параметры страницы: размер страницы — от 1 до {limit}, номер страницы не может быть отрицательным.|Бет параметрлері дұрыс емес: бет өлшемі — 1-ден {limit} дейін, бет нөмірі теріс бола алмайды.|dictionaries|—|needs review|
|dictionaries.invalid_query_syntax|400|Выражение «{field}» не удалось разобрать. Проверьте синтаксис.|«{field}» өрнегін талдау мүмкін болмады. Синтаксисті тексеріңіз.|dictionaries|—|needs review|
|dictionaries.languages.already_exists|409|Язык с кодом «{code}» уже существует.|Коды «{code}» тіл бұрыннан бар.|dictionaries|—|needs review|
|dictionaries.languages.default_cannot_be_deactivated|409|Нельзя деактивировать язык по умолчанию.|Әдепкі тілді белсенді емес етуге болмайды.|dictionaries|—|needs review|
|dictionaries.languages.default_must_be_active|409|Языком по умолчанию может стать только активный язык (код «{code}»).|Әдепкі тіл тек белсенді тіл бола алады (коды «{code}»).|dictionaries|—|needs review|
|dictionaries.languages.endonym_required|400|У активного языка должно быть указано название на этом языке.|Белсенді тілдің осы тілдегі атауы көрсетілуі керек.|dictionaries|—|needs review|
|dictionaries.languages.invalid_code|400|Код языка должен быть длиной от 2 до 8 символов в формате BCP-47 (например, «ru» или «kk-KZ»).|Тіл коды 2-ден 8 таңбаға дейін, BCP-47 форматында болуы керек (мысалы, «ru» немесе «kk-KZ»).|dictionaries|—|needs review|
|dictionaries.languages.not_found|404|Язык не найден.|Тіл табылмады.|dictionaries|—|needs review|
|dictionaries.unknown_query_field|400|Поле «{field}» нельзя использовать для фильтрации или сортировки.|«{field}» өрісін сүзу немесе сұрыптау үшін пайдалануға болмайды.|dictionaries|—|needs review|
|dictionaries.validation_failed|400|Проверьте правильность заполнения полей.|Өрістердің дұрыс толтырылғанын тексеріңіз.|dictionaries|—|needs review|
|dictionaries.value_required|400|Обязательное поле.|Міндетті өріс.|dictionaries|—|needs review|
|dictionaries.value_too_long|400|Превышена максимальная длина: {limit} символов.|Ең үлкен ұзындықтан асып кетті: {limit} таңба.|dictionaries|—|needs review|
|dictionaries.version_conflict|409|Запись уже изменена другим пользователем. Обновите данные и повторите попытку.|Жазбаны басқа пайдаланушы өзгертіп үлгерді. Деректерді жаңартып, әрекетті қайталаңыз.|dictionaries|—|needs review|
|dictionaries.version_required|400|Для изменения существующей записи нужен заголовок If-Match.|Бар жазбаны өзгерту үшін If-Match тақырыбы қажет.|dictionaries|—|needs review|
|http.bad_request|400|Ошибка валидации запроса.|Сұраныс дерегінде валидация қатесі бар.|assigned by HTTP status|—|needs review|
|http.conflict|409|Конфликт состояния ресурса.|Ресурс күйінің қайшылығы.|assigned by HTTP status|—|needs review|
|http.forbidden|403|Доступ запрещён.|Кіруге тыйым салынған.|assigned by HTTP status|—|needs review|
|http.internal_error|500|Внутренняя ошибка сервера. Обратитесь к администратору, указав traceId.|Сервердің ішкі қатесі. Әкімшіге traceId көрсетіп хабарласыңыз.|assigned by HTTP status|—|needs review|
|http.method_not_allowed|405|Метод не поддерживается для этого маршрута.|Бұл маршрут үшін әдіске қолдау көрсетілмейді.|assigned by HTTP status|—|needs review|
|http.not_found|404|Ресурс не найден.|Ресурс табылмады.|assigned by HTTP status|—|needs review|
|http.request_failed|any|Не удалось обработать запрос.|Сұранысты өңдеу мүмкін болмады.|assigned by HTTP status|—|needs review|
|http.service_unavailable|503|Сервис временно недоступен, повторите позже.|Қызмет уақытша қолжетімсіз, кейінірек қайталаңыз.|assigned by HTTP status|—|needs review|
|http.unauthorized|401|Требуется аутентификация.|Аутентификация қажет.|assigned by HTTP status|—|needs review|
|organizations.organization.not_found|404|Организация не найдена.|Ұйым табылмады.|organizations|—|needs review|
|session.anonymous|401|Вы не вошли в систему.|Сіз жүйеге кірмегенсіз.|session|—|needs review|
|session.expired|401|Сессия истекла. Войдите заново.|Сессия мерзімі аяқталды. Қайта кіріңіз.|session|—|needs review|
|session.invalid_return_url|400|Недопустимый адрес возврата после входа.|Кіргеннен кейінгі қайту мекенжайы жарамсыз.|session|—|needs review|
|user_directory.push_malformed|400|Запрос канала пользователей не разобран: тело повреждено или слишком велико.|Пайдаланушылар арнасының сұранысы талданбады: денесі бүлінген немесе тым үлкен.|user_directory|—|needs review|
|user_directory.push_unauthorized|401|Запрос канала пользователей отклонён: подпись отсутствует, неверна или устарела.|Пайдаланушылар арнасының сұранысы қабылданбады: қолтаңба жоқ, қате немесе ескірген.|user_directory|—|needs review|
|user_directory.snapshot_conflict|409|Сверка с этим идентификатором уже начата с другим моментом или устарела.|Осы идентификатормен салыстыру басқа сәтпен басталған немесе ескірген.|user_directory|—|needs review|
|user_directory.snapshot_incomplete|409|Сверка пользователей не завершена: получено записей — {received} из {expected}.|Пайдаланушыларды салыстыру аяқталмады: {expected} жазбаның {received} алынды.|user_directory|—|needs review|
|user_directory.snapshot_not_found|404|Сверка пользователей не найдена.|Пайдаланушыларды салыстыру табылмады.|user_directory|—|needs review|
|user_directory.snapshot_not_open|409|Сверка пользователей уже завершена или заменена более новой.|Пайдаланушыларды салыстыру аяқталған немесе жаңасымен ауыстырылған.|user_directory|—|needs review|
|user_directory.too_many_entries|400|Слишком много записей в одном запросе: не больше {limit}.|Бір сұраныста жазбалар тым көп: {limit} аспауы керек.|user_directory|—|needs review|
|user_directory.too_many_ids|400|Слишком много идентификаторов в одном запросе: не больше {limit}.|Бір сұраныста идентификаторлар тым көп: {limit} аспауы керек.|user_directory|—|needs review|
|user_directory.validation_failed|400|Проверьте правильность переданных данных.|Берілген деректердің дұрыстығын тексеріңіз.|user_directory|—|needs review|
|user_directory.value_invalid|400|Недопустимое значение.|Жарамсыз мән.|user_directory|—|needs review|
|user_directory.value_required|400|Обязательное значение.|Міндетті мән.|user_directory|—|needs review|
|user_directory.value_too_long|400|Превышена максимальная длина: {limit} символов.|Ең үлкен ұзындықтан асып кетті: {limit} таңба.|user_directory|—|needs review|
