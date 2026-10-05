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
|organizations.area.is_team_default|409|Область используется как область по умолчанию команды «{team}»|Аймақ «{team}» командасының әдепкі аймағы ретінде пайдаланылады|organizations|—|needs review|
|organizations.area.move_into_own_subtree|409|Нельзя переместить область внутрь неё самой или её вложенной области|Аймақты өзінің ішіне немесе оның ішкі аймағына жылжытуға болмайды|organizations|—|needs review|
|organizations.area.name_taken|409|Область с таким названием уже есть на этом уровне|Бұл деңгейде мұндай атауы бар аймақ бар|organizations|—|needs review|
|organizations.area.not_empty|409|Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области|Жұмыс элементтері (жойылғандарын қоса) немесе ішкі аймақтары бар аймақты жоюға болмайды|organizations|—|needs review|
|organizations.area.not_found|404|Область не найдена.|Аймақ табылмады.|organizations|—|needs review|
|organizations.area.root_is_fixed|409|Корневая область меняется только вместе с проектом|Түбірлік аймақ тек жобамен бірге өзгереді|organizations|—|needs review|
|organizations.name_length|400|Название должно содержать от 1 до 100 символов|Атауы 1-ден 100 таңбаға дейін болуы керек|organizations|—|needs review|
|organizations.organization.create_failed|500|Не удалось создать организацию. Попробуйте ещё раз|Ұйымды құру мүмкін болмады. Қайталап көріңіз|organizations|—|needs review|
|organizations.organization.create_forbidden|403|Недостаточно прав|Құқықтар жеткіліксіз|organizations|—|needs review|
|organizations.organization.name_taken|409|Название «{name}» уже занято|«{name}» атауы бос емес|organizations|—|needs review|
|organizations.organization.not_found|404|Организация не найдена.|Ұйым табылмады.|organizations|—|needs review|
|organizations.project.key_invalid|400|Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы|Жоба кілті: әріптен басталатын 2-ден 10-ға дейін латын бас әріптері мен цифрлар|organizations|—|needs review|
|organizations.project.key_required|400|Укажите ключ проекта|Жоба кілтін көрсетіңіз|organizations|—|needs review|
|organizations.project.key_taken|409|Ключ «{key}» уже используется в организации|«{key}» кілті ұйымда қолданылып жүр|organizations|—|needs review|
|organizations.project.name_taken|409|Проект с таким названием уже есть в организации|Ұйымда мұндай атауы бар жоба бар|organizations|—|needs review|
|organizations.project.name_taken_by_deactivated|409|Проект с таким названием уже есть в организации: он деактивирован|Ұйымда мұндай атауы бар жоба бар: ол сөндірілген|organizations|—|needs review|
|organizations.project.not_found|404|Проект не найден.|Жоба табылмады.|organizations|—|needs review|
|organizations.team.default_area_not_owned|409|Область по умолчанию должна принадлежать самой команде|Әдепкі аймақ команданың өзіне тиесілі болуы керек|organizations|—|needs review|
|organizations.team.first_team_cannot_be_deleted|409|Нельзя удалить команду, которой принадлежит корневая область проекта|Жобаның түбірлік аймағы тиесілі команданы жоюға болмайды|organizations|—|needs review|
|organizations.team.first_team_default_fixed|409|У первой команды область по умолчанию не меняется|Бірінші команданың әдепкі аймағы өзгермейді|organizations|—|needs review|
|organizations.team.name_taken|409|Команда с таким названием уже есть в проекте|Жобада мұндай атауы бар команда бар|organizations|—|needs review|
|organizations.team.not_found|404|Команда не найдена.|Команда табылмады.|organizations|—|needs review|
|organizations.validation_failed|400|Проверьте правильность заполнения полей.|Өрістердің дұрыс толтырылғанын тексеріңіз.|organizations|—|needs review|
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
|workitems.invalid_page|400|Некорректный номер страницы.|Бет нөмірі дұрыс емес.|workitems|—|needs review|
|workitems.validation_failed|400|Проверьте правильность заполнения полей.|Өрістердің дұрыс толтырылғанын тексеріңіз.|workitems|—|needs review|
|workitems.workitem.assignee_unavailable|409|Этого пользователя нельзя назначить исполнителем.|Бұл пайдаланушыны орындаушы етіп тағайындауға болмайды.|workitems|—|needs review|
|workitems.workitem.description_too_long|400|Не более 100 000 символов.|100 000 таңбадан аспауы керек.|workitems|—|needs review|
|workitems.workitem.not_found|404|Рабочий элемент не найден.|Жұмыс элементі табылмады.|workitems|—|needs review|
|workitems.workitem.request_id_invalid|400|Не удалось отправить форму. Обновите страницу и попробуйте ещё раз.|Пішінді жіберу мүмкін болмады. Бетті жаңартып, қайталап көріңіз.|workitems|—|needs review|
|workitems.workitem.state_not_allowed|409|Этот статус недоступен для рабочего элемента этого типа.|Бұл күй осы түрдегі жұмыс элементі үшін қолжетімсіз.|workitems|—|needs review|
|workitems.workitem.title_multiline|400|Заголовок должен быть в одну строку.|Тақырып бір жолда болуы керек.|workitems|—|needs review|
|workitems.workitem.title_required|400|Введите заголовок.|Тақырыпты енгізіңіз.|workitems|—|needs review|
|workitems.workitem.title_too_long|400|Не более 255 символов.|255 таңбадан аспауы керек.|workitems|—|needs review|
|workitems.workitem.type_not_found|404|Тип рабочего элемента не найден.|Жұмыс элементінің түрі табылмады.|workitems|—|needs review|
