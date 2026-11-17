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
|boards.backlog.position_invalid|400|Некорректная позиция перемещения.|Жылжыту орны дұрыс емес.|boards|—|needs review|
|boards.backlog.reorder_stale|409|Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.|Жылжыту мүмкін болмады: бэклог өзгерді. Тізім жаңартылды — жылжытуды қайталаңыз.|boards|—|needs review|
|boards.invalid_cursor|400|Некорректный курсор страницы.|Бет курсоры дұрыс емес.|boards|—|needs review|
|boards.invalid_grouping|400|Такая группировка для этого уровня недоступна.|Бұл деңгей үшін мұндай топтау қолжетімсіз.|boards|—|needs review|
|boards.invalid_parameter|400|Некорректное значение параметра запроса.|Сұраныс параметрінің мәні дұрыс емес.|boards|—|needs review|
|boards.level.not_found|404|Уровень бэклога не найден.|Бэклог деңгейі табылмады.|boards|—|needs review|
|boards.validation_failed|400|Проверьте правильность запроса.|Сұраудың дұрыстығын тексеріңіз.|boards|—|needs review|
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
|http.too_many_requests|429|Сервис сейчас занят. Повторите запрос через несколько секунд.|Қызмет қазір бос емес. Сұранысты бірнеше секундтан кейін қайталаңыз.|assigned by HTTP status|—|needs review|
|http.unauthorized|401|Требуется аутентификация.|Аутентификация қажет.|assigned by HTTP status|—|needs review|
|imports.access_forbidden|403|У вас нет прав на эту операцию.|Бұл әрекетке құқығыңыз жоқ.|imports|—|needs review|
|imports.dry_run.empty_file|any|В файле нет строк данных.|Файлда деректер жолдары жоқ.|imports|—|needs review|
|imports.dry_run.no_identifier_column|any|Без столбца идентификатора каждая строка создаёт новый элемент.|Сәйкестендіргіш бағаны болмаса, әр жол жаңа элемент жасайды.|imports|—|needs review|
|imports.dry_run.not_found|404|Результат проверки не найден или устарел. Проверьте файл снова.|Тексеру нәтижесі табылмады немесе ескірді. Файлды қайта тексеріңіз.|imports|—|needs review|
|imports.file.columns_missing|400|В файле нет столбцов, которые указаны в шаблоне: {columns}.|Файлда үлгіде көрсетілген бағандар жоқ: {columns}.|imports|—|needs review|
|imports.file.expands_too_much|400|Файл слишком велик после распаковки и не принят.|Файл ашылғаннан кейін тым үлкен болып, қабылданбады.|imports|—|needs review|
|imports.file.external_reference|400|Файл ссылается на другой файл и не принят.|Файл басқа файлға сілтейді, сондықтан қабылданбады.|imports|—|needs review|
|imports.file.has_macros|400|В файле есть макросы, такой файл не принят.|Файлда макростар бар, мұндай файл қабылданбайды.|imports|—|needs review|
|imports.file.not_found|404|Файл не найден или срок его хранения истёк. Загрузите его снова.|Файл табылмады немесе сақтау мерзімі аяқталды. Оны қайта жүктеңіз.|imports|—|needs review|
|imports.file.refused|400|Файл не принят.|Файл қабылданбады.|imports|—|needs review|
|imports.file.sheet_missing|400|В файле нет листа «{sheet}», указанного в шаблоне.|Файлда үлгіде көрсетілген «{sheet}» парағы жоқ.|imports|—|needs review|
|imports.file.too_large|400|Файл больше 10 МБ.|Файл 10 МБ-тан үлкен.|imports|—|needs review|
|imports.file.too_many_columns|400|В файле больше 200 столбцов.|Файлда 200-ден астам баған бар.|imports|—|needs review|
|imports.file.too_many_rows|400|В файле больше 5 000 строк данных. Разделите его на части.|Файлда 5 000-нан астам деректер жолы бар. Оны бөліктерге бөліңіз.|imports|—|needs review|
|imports.file.too_many_sheets|400|В книге больше 50 листов.|Кітапта 50-ден астам парақ бар.|imports|—|needs review|
|imports.file.unreadable|400|Файл не удалось прочитать. Проверьте, что он не повреждён.|Файлды оқу мүмкін болмады. Оның зақымдалмағанын тексеріңіз.|imports|—|needs review|
|imports.file.unsupported_format|400|Поддерживаются файлы .xlsx и .csv.|.xlsx және .csv файлдарына қолдау көрсетіледі.|imports|—|needs review|
|imports.mapping.chain_too_long|400|В цепочке не больше 10 преобразований.|Тізбекте 10-нан артық түрлендіру болмауы керек.|imports|—|needs review|
|imports.mapping.clear_not_allowed|400|Для этого поля нельзя очищать значение.|Бұл өрістің мәнін тазалауға болмайды.|imports|—|needs review|
|imports.mapping.column_unknown|400|В файле нет столбца «{column}».|Файлда «{column}» бағаны жоқ.|imports|—|needs review|
|imports.mapping.converter_param_invalid|400|Укажите значение.|Мәнді көрсетіңіз.|imports|—|needs review|
|imports.mapping.converter_unknown|400|Неизвестное преобразование.|Белгісіз түрлендіру.|imports|—|needs review|
|imports.mapping.field_taken|400|Это поле уже сопоставлено.|Бұл өріс бұрын сәйкестендірілген.|imports|—|needs review|
|imports.mapping.field_unknown|400|Это поле не поддерживается.|Бұл өріс қолдау көрсетілмейді.|imports|—|needs review|
|imports.mapping.id_taken|400|Идентификаторы сопоставлений в шаблоне должны быть разными.|Үлгідегі сәйкестендірулердің идентификаторлары әртүрлі болуы керек.|imports|—|needs review|
|imports.mapping.list_into_single|400|Поле «{field}» хранит одно значение: уберите «Разделить» из цепочки.|«{field}» өрісі бір мәнді сақтайды: тізбектен «Бөлу» әрекетін алып тастаңыз.|imports|—|needs review|
|imports.mapping.pattern_invalid|400|Некорректное регулярное выражение.|Тұрақты өрнек дұрыс емес.|imports|—|needs review|
|imports.mapping.pattern_too_long|400|Шаблон — не больше 500 символов.|Үлгі — 500 таңбадан көп емес.|imports|—|needs review|
|imports.mapping.source_required|400|Укажите столбец или константу.|Бағанды немесе тұрақтыны көрсетіңіз.|imports|—|needs review|
|imports.recoding.default_required|400|Выберите значение.|Мәнді таңдаңыз.|imports|—|needs review|
|imports.recoding.empty_not_allowed|400|Для этого поля нельзя оставить пустым.|Бұл өрісті бос қалдыруға болмайды.|imports|—|needs review|
|imports.recoding.target_unknown|400|Значение больше недоступно. Выберите другое.|Мән енді қолжетімді емес. Басқасын таңдаңыз.|imports|—|needs review|
|imports.recoding.too_many_entries|400|В таблице не больше 1 000 значений: столбец с большим числом разных значений — свободный текст.|Кестеде 1 000-нан артық мән болмауы керек: мәндері көп баған — еркін мәтін.|imports|—|needs review|
|imports.recoding.too_many_tables|400|Для одного сопоставления — одна таблица перекодировки.|Бір сәйкестендіруге — бір қайта кодтау кестесі.|imports|—|needs review|
|imports.row.assignee_not_found|any|Исполнитель не найден или не имеет доступа к проекту.|Орындаушы табылмады немесе жобаға қолжетімділігі жоқ.|imports|—|needs review|
|imports.row.convert_failed|any|Не удалось преобразовать: столбец «{column}», ячейка {cell}, значение «{value}», преобразование «{converter}».|Түрлендіру мүмкін болмады: «{column}» бағаны, {cell} ұяшығы, «{value}» мәні, «{converter}» түрлендіруі.|imports|—|needs review|
|imports.row.date_out_of_range|any|Укажите дату с 2000 по 2099 год.|2000 жылдан 2099 жылға дейінгі күнді көрсетіңіз.|imports|—|needs review|
|imports.row.empty|any|Пустая строка.|Бос жол.|imports|—|needs review|
|imports.row.field_not_on_type|any|Это поле недоступно для рабочего элемента этого типа.|Бұл өріс осы түрдегі жұмыс элементі үшін қолжетімсіз.|imports|—|needs review|
|imports.row.identifier_deleted|any|Элемент находится в корзине.|Элемент себетте.|imports|—|needs review|
|imports.row.identifier_duplicate|any|Тот же идентификатор в строке {row}.|Дәл сол сәйкестендіргіш {row}-жолда.|imports|—|needs review|
|imports.row.identifier_not_found|any|Элемент не найден.|Элемент табылмады.|imports|—|needs review|
|imports.row.identifier_skipped|any|Строка без идентификатора пропущена по настройке шаблона.|Сәйкестендіргіші жоқ жол үлгі параметрі бойынша өткізілді.|imports|—|needs review|
|imports.row.project_changed|any|Проект элемента нельзя изменить импортом.|Элементтің жобасын импортпен өзгертуге болмайды.|imports|—|needs review|
|imports.row.project_forbidden|any|Нет прав на импорт в этот проект.|Бұл жобаға импорттауға құқық жоқ.|imports|—|needs review|
|imports.row.project_not_found|any|Проект не найден.|Жоба табылмады.|imports|—|needs review|
|imports.row.recoding_outdated|any|Значение MUSTASK, выбранное для «{value}», больше недоступно.|«{value}» үшін таңдалған MUSTASK мәні енді қолжетімді емес.|imports|—|needs review|
|imports.row.regex_timeout|any|Шаблон выполняется слишком долго.|Үлгі тым ұзақ орындалады.|imports|—|needs review|
|imports.row.required_missing|any|Не заполнено обязательное поле «{field}».|«{field}» міндетті өрісі толтырылмаған.|imports|—|needs review|
|imports.row.state_not_found|any|Состояние не найдено среди состояний этого типа.|Күй осы түрдің күйлері арасында табылмады.|imports|—|needs review|
|imports.row.team_not_linked|any|Команда не участвует в проекте.|Команда жобаға қатыспайды.|imports|—|needs review|
|imports.row.type_changed|any|Тип элемента нельзя изменить импортом.|Элементтің түрін импортпен өзгертуге болмайды.|imports|—|needs review|
|imports.row.type_not_found|any|Тип рабочего элемента не найден.|Жұмыс элементінің түрі табылмады.|imports|—|needs review|
|imports.row.value_invalid|any|Недопустимое значение поля «{field}».|«{field}» өрісінің мәні жарамсыз.|imports|—|needs review|
|imports.row.value_not_recoded|any|Значение «{value}» не сопоставлено ни с одним значением MUSTASK.|«{value}» мәні MUSTASK-тың ешбір мәнімен сәйкестендірілмеген.|imports|—|needs review|
|imports.row.value_truncated|any|Значение поля «{field}» обрезано до допустимой длины.|«{field}» өрісінің мәні рұқсат етілген ұзындыққа дейін қысқартылды.|imports|—|needs review|
|imports.source.first_data_row_invalid|400|Первая строка данных должна быть ниже строки заголовков.|Деректердің бірінші жолы тақырыптар жолынан төмен болуы керек.|imports|—|needs review|
|imports.source.header_empty|400|В этой строке нет заголовков. Выберите другую строку.|Бұл жолда тақырыптар жоқ. Басқа жолды таңдаңыз.|imports|—|needs review|
|imports.source.header_row_invalid|400|Строка заголовков — число от 1 до 50.|Тақырыптар жолы — 1-ден 50-ге дейінгі сан.|imports|—|needs review|
|imports.template.limit_reached|409|В организации уже 100 шаблонов — это предел. Удалите ненужный шаблон.|Ұйымда 100 үлгі бар — бұл шектеу. Қажетсіз үлгіні жойыңыз.|imports|—|needs review|
|imports.template.name_required|400|Укажите название.|Атауын көрсетіңіз.|imports|—|needs review|
|imports.template.name_taken|409|Шаблон с таким названием уже есть.|Мұндай атаумен үлгі бар.|imports|—|needs review|
|imports.template.name_too_long|400|Название — не больше 100 символов.|Атау — 100 таңбадан көп емес.|imports|—|needs review|
|imports.template.not_found|404|Шаблон не найден.|Үлгі табылмады.|imports|—|needs review|
|imports.template.too_many_headers|400|В шаблоне не больше 200 столбцов файла.|Үлгіде файлдың 200-ден артық бағаны болмауы керек.|imports|—|needs review|
|imports.validation_failed|400|Проверьте правильность заполнения полей.|Өрістердің дұрыс толтырылғанын тексеріңіз.|imports|—|needs review|
|iterations.sprint.active_dates_required|409|У активного спринта должны быть обе даты.|Белсенді спринттің екі күні де болуы керек.|iterations|—|needs review|
|iterations.sprint.active_exists|409|У команды уже есть активный спринт.|Команданың белсенді спринті бұрыннан бар.|iterations|—|needs review|
|iterations.sprint.close_destination_invalid|409|Незавершённые элементы можно перенести только в запланированный спринт этой команды.|Аяқталмаған элементтерді тек осы команданың жоспарланған спринтіне ауыстыруға болады.|iterations|—|needs review|
|iterations.sprint.closed_dates_locked|409|Даты завершённого спринта изменить нельзя.|Аяқталған спринттің күндерін өзгертуге болмайды.|iterations|—|needs review|
|iterations.sprint.closed_target|409|Спринт завершён: в него нельзя добавить элемент.|Спринт аяқталған: оған элемент қосуға болмайды.|iterations|—|needs review|
|iterations.sprint.dates_reversed|400|Дата окончания не может быть раньше даты начала.|Аяқталу күні басталу күнінен бұрын болмауы керек.|iterations|—|needs review|
|iterations.sprint.dates_unpaired|400|Укажите обе даты спринта или ни одной.|Спринттің екі күнін де көрсетіңіз немесе ешқайсысын көрсетпеңіз.|iterations|—|needs review|
|iterations.sprint.name_invalid|400|Название спринта — от 1 до 100 символов.|Спринт атауы — 1-ден 100 символға дейін.|iterations|—|needs review|
|iterations.sprint.name_taken|409|Спринт с таким названием уже есть в команде.|Командада мұндай атаумен спринт бұрыннан бар.|iterations|—|needs review|
|iterations.sprint.not_active|409|Завершить можно только активный спринт.|Тек белсенді спринтті аяқтауға болады.|iterations|—|needs review|
|iterations.sprint.not_found|404|Спринт не найден.|Спринт табылмады.|iterations|—|needs review|
|iterations.sprint.not_planned|409|Начать можно только запланированный спринт.|Тек жоспарланған спринтті бастауға болады.|iterations|—|needs review|
|iterations.sprint.not_removable|409|Удалить можно только запланированный спринт без элементов.|Тек элементтері жоқ жоспарланған спринтті жоюға болады.|iterations|—|needs review|
|iterations.sprint.other_team|409|Спринт принадлежит другой команде.|Спринт басқа командаға тиесілі.|iterations|—|needs review|
|iterations.sprint.start_dates_required|409|Чтобы начать спринт, укажите обе даты.|Спринтті бастау үшін екі күнді де көрсетіңіз.|iterations|—|needs review|
|iterations.sprint.team_required|409|Сначала выберите команду: спринт есть только у команды.|Алдымен команданы таңдаңыз: спринт тек командада болады.|iterations|—|needs review|
|iterations.validation_failed|400|Проверьте правильность запроса.|Сұраудың дұрыстығын тексеріңіз.|iterations|—|needs review|
|organizations.access.forbidden|403|Недостаточно прав|Құқықтар жеткіліксіз|organizations|—|needs review|
|organizations.area.depth_exceeded|409|Дерево областей не может быть глубже 10 уровней|Аймақ ағашы 10 деңгейден терең болмауы керек|organizations|—|needs review|
|organizations.area.has_teams|409|Нельзя удалить департамент, в котором есть команды|Командалары бар департаментті жоюға болмайды|organizations|—|needs review|
|organizations.area.is_team_default|409|Область используется как область по умолчанию команды «{team}»|Аймақ «{team}» командасының әдепкі аймағы ретінде пайдаланылады|organizations|yes|needs review|
|organizations.area.link_type_not_allowed|409|Команду можно связать только с департаментом. Команды проекта подключаются на странице проекта.|Команданы тек департаментпен байланыстыруға болады. Жоба командалары жоба бетінде қосылады.|organizations|yes|needs review|
|organizations.area.move_into_own_subtree|409|Нельзя переместить область внутрь неё самой или её вложенной области|Аймақты өзінің ішіне немесе оның ішкі аймағына жылжытуға болмайды|organizations|—|needs review|
|organizations.area.name_taken|409|Область с таким названием уже есть на этом уровне|Бұл деңгейде мұндай атауы бар аймақ бар|organizations|—|needs review|
|organizations.area.not_empty|409|Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы|Жобалары, ішкі аймақтары немесе жұмыс элементтері бар аймақты жоюға болмайды|organizations|—|needs review|
|organizations.area.not_found|404|Область не найдена.|Аймақ табылмады.|organizations|—|needs review|
|organizations.area.outside_project|409|Вложенную область нельзя переместить за пределы её проекта|Ішкі аймақты өз жобасының шегінен тыс жылжытуға болмайды|organizations|—|needs review|
|organizations.area.root_is_fixed|409|Корневая область меняется только вместе с проектом|Түбірлік аймақ тек жобамен бірге өзгереді|organizations|—|needs review|
|organizations.area.type_not_allowed|409|Область этого типа нельзя разместить в выбранном месте. Департамент стоит на верхнем уровне, проект — на верхнем уровне или в департаменте, вложенная область — внутри своего проекта.|Мұндай түрдегі аймақты таңдалған жерге орналастыруға болмайды. Департамент жоғарғы деңгейде, жоба — жоғарғы деңгейде немесе департаментте, ішкі аймақ — өз жобасының ішінде тұрады.|organizations|—|needs review|
|organizations.description_length|400|Описание должно содержать не более 2000 символов|Сипаттама 2000 таңбадан аспауы керек|organizations|—|needs review|
|organizations.grant.changed|409|Доступ уже изменён или отозван. Список обновлён.|Қолжетімділік бұрын өзгертілген немесе қайтарып алынған. Тізім жаңартылды.|organizations|—|needs review|
|organizations.grant.role_invalid|400|Выберите роль: читатель, участник или менеджер|Рөлді таңдаңыз: оқырман, қатысушы немесе менеджер|organizations|—|needs review|
|organizations.grant.scope_not_allowed|409|Эту роль нельзя выдать на этом объекте.|Бұл рөлді осы нысанда беруге болмайды.|organizations|—|needs review|
|organizations.grant.self_grant|400|Нельзя выдать доступ самому себе.|Өзіңізге қолжетімділік беруге болмайды.|organizations|—|needs review|
|organizations.membership.already_member|409|Пользователь уже состоит в организации|Пайдаланушы ұйымның құрамында бұрыннан бар|organizations|—|needs review|
|organizations.membership.last_admin|409|В организации должен остаться хотя бы один администратор|Ұйымда кемінде бір әкімші қалуы керек|organizations|—|needs review|
|organizations.membership.not_found|404|Участник не найден.|Мүше табылмады.|organizations|—|needs review|
|organizations.membership.role_invalid|400|Выберите роль: администратор, участник или читатель|Рөлді таңдаңыз: әкімші, қатысушы немесе оқырман|organizations|—|needs review|
|organizations.membership.user_invalid|400|Укажите пользователя|Пайдаланушыны көрсетіңіз|organizations|—|needs review|
|organizations.membership.user_unavailable|409|Этого пользователя нельзя добавить в организацию|Бұл пайдаланушыны ұйымға қосуға болмайды|organizations|—|needs review|
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
|organizations.rescue.already_admin|409|Пользователь уже администратор этой организации|Пайдаланушы бұл ұйымның әкімшісі болып табылады|organizations|—|needs review|
|organizations.rescue.forbidden|403|Недостаточно прав|Құқықтар жеткіліксіз|organizations|—|needs review|
|organizations.rescue.invalid_page|400|Некорректный номер страницы.|Бет нөмірі дұрыс емес.|organizations|—|needs review|
|organizations.rescue.reason_invalid|400|Причина содержит недопустимые символы|Себепте жарамсыз таңбалар бар|organizations|—|needs review|
|organizations.rescue.reason_required|400|Укажите причину|Себебін көрсетіңіз|organizations|—|needs review|
|organizations.rescue.reason_too_long|400|Не более 500 символов|500 таңбадан аспауы керек|organizations|—|needs review|
|organizations.rescue.self_appointment|409|Нельзя назначить администратором себя|Өзіңізді әкімші етіп тағайындауға болмайды|organizations|—|needs review|
|organizations.rescue.user_unavailable|409|Этого пользователя нельзя назначить администратором|Бұл пайдаланушыны әкімші етіп тағайындауға болмайды|organizations|—|needs review|
|organizations.team.default_area_not_owned|409|Область по умолчанию должна принадлежать самой команде|Әдепкі аймақ команданың өзіне тиесілі болуы керек|organizations|yes|needs review|
|organizations.team.department_required|400|Выберите департамент|Департаментті таңдаңыз|organizations|—|needs review|
|organizations.team.destination_required|400|Выберите команду или «Без команды»|Команданы немесе «Командасыз» таңдаңыз|organizations|—|needs review|
|organizations.team.first_team_cannot_be_deleted|409|Нельзя удалить команду, которой принадлежит корневая область проекта|Жобаның түбірлік аймағы тиесілі команданы жоюға болмайды|organizations|yes|needs review|
|organizations.team.first_team_default_fixed|409|У первой команды область по умолчанию не меняется|Бірінші команданың әдепкі аймағы өзгермейді|organizations|yes|needs review|
|organizations.team.has_links|409|Команда связана с проектами. Отключите её от них перед перемещением.|Команда жобалармен байланысты. Көшірмес бұрын оны жобалардан ажыратыңыз.|organizations|—|needs review|
|organizations.team.last_team_cannot_be_deleted|409|Нельзя удалить единственную команду проекта.|Жобаның жалғыз командасын жоюға болмайды.|organizations|yes|needs review|
|organizations.team.member_exists|409|Пользователь уже состоит в команде|Пайдаланушы командада бұрыннан бар|organizations|—|needs review|
|organizations.team.member_not_found|404|Участник команды не найден.|Команда мүшесі табылмады.|organizations|—|needs review|
|organizations.team.member_not_in_organization|409|Добавить в команду можно только активного участника организации|Командаға ұйымның тек белсенді мүшесін ғана қосуға болады|organizations|—|needs review|
|organizations.team.name_taken|409|Команда с таким названием уже есть в организации|Ұйымда мұндай атауы бар команда бар|organizations|—|needs review|
|organizations.team.not_found|404|Команда не найдена.|Команда табылмады.|organizations|—|needs review|
|organizations.team.not_in_project|409|Команда не участвует в проекте.|Команда жобаға қатыспайды.|organizations|—|needs review|
|organizations.validation_failed|400|Проверьте правильность заполнения полей.|Өрістердің дұрыс толтырылғанын тексеріңіз.|organizations|—|needs review|
|search.invalid_group|400|Такой группы результатов нет.|Мұндай нәтиже тобы жоқ.|search|—|needs review|
|search.query_too_long|400|Запрос не должен быть длиннее 100 символов.|Сұраныс 100 таңбадан ұзын болмауы керек.|search|—|needs review|
|search.query_too_short|400|Введите не менее 2 символов.|Кемінде 2 таңба енгізіңіз.|search|—|needs review|
|search.validation_failed|400|Проверьте правильность запроса.|Сұраныстың дұрыстығын тексеріңіз.|search|—|needs review|
|session.anonymous|401|Вы не вошли в систему.|Сіз жүйеге кірмегенсіз.|session|—|needs review|
|session.expired|401|Сессия истекла. Войдите заново.|Сессия мерзімі аяқталды. Қайта кіріңіз.|session|—|needs review|
|session.invalid_return_url|400|Недопустимый адрес возврата после входа.|Кіргеннен кейінгі қайту мекенжайы жарамсыз.|session|—|needs review|
|session.no_access|403|У вашей учётной записи нет доступа к MUSTASK. Обратитесь к администратору.|Сіздің тіркелгіңізде MUSTASK-қа қолжетімділік жоқ. Әкімшіге хабарласыңыз.|session|—|needs review|
|user_directory.push_malformed|400|Запрос канала пользователей не разобран: тело повреждено или слишком велико.|Пайдаланушылар арнасының сұранысы талданбады: денесі бүлінген немесе тым үлкен.|user_directory|yes|needs review|
|user_directory.push_unauthorized|401|Запрос канала пользователей отклонён: подпись отсутствует, неверна или устарела.|Пайдаланушылар арнасының сұранысы қабылданбады: қолтаңба жоқ, қате немесе ескірген.|user_directory|yes|needs review|
|user_directory.snapshot_conflict|409|Сверка с этим идентификатором уже начата с другим моментом или устарела.|Осы идентификатормен салыстыру басқа сәтпен басталған немесе ескірген.|user_directory|yes|needs review|
|user_directory.snapshot_incomplete|409|Сверка пользователей не завершена: получено записей — {received} из {expected}.|Пайдаланушыларды салыстыру аяқталмады: {expected} жазбаның {received} алынды.|user_directory|yes|needs review|
|user_directory.snapshot_not_found|404|Сверка пользователей не найдена.|Пайдаланушыларды салыстыру табылмады.|user_directory|yes|needs review|
|user_directory.snapshot_not_open|409|Сверка пользователей уже завершена или заменена более новой.|Пайдаланушыларды салыстыру аяқталған немесе жаңасымен ауыстырылған.|user_directory|yes|needs review|
|user_directory.too_many_entries|400|Слишком много записей в одном запросе: не больше {limit}.|Бір сұраныста жазбалар тым көп: {limit} аспауы керек.|user_directory|yes|needs review|
|user_directory.too_many_ids|400|Слишком много идентификаторов в одном запросе: не больше {limit}.|Бір сұраныста идентификаторлар тым көп: {limit} аспауы керек.|user_directory|—|needs review|
|user_directory.validation_failed|400|Проверьте правильность переданных данных.|Берілген деректердің дұрыстығын тексеріңіз.|user_directory|—|needs review|
|user_directory.value_invalid|400|Недопустимое значение.|Жарамсыз мән.|user_directory|—|needs review|
|user_directory.value_required|400|Обязательное значение.|Міндетті мән.|user_directory|—|needs review|
|user_directory.value_too_long|400|Превышена максимальная длина: {limit} символов.|Ең үлкен ұзындықтан асып кетті: {limit} таңба.|user_directory|—|needs review|
|workitems.invalid_page|400|Некорректный номер страницы.|Бет нөмірі дұрыс емес.|workitems|—|needs review|
|workitems.link.cycle|409|Нельзя выбрать родителем {parent}: он вложен в {item} ({path}).|{parent} элементін ата-аналық ретінде таңдауға болмайды: ол {item} ішіне енгізілген ({path}).|workitems|—|needs review|
|workitems.link.depth_exceeded|409|Слишком глубокая вложенность: в цепочке родителей может быть не больше {limit} элементов.|Кірістіру тым терең: ата-аналық элементтер тізбегінде {limit} элементтен артық болмауы керек.|workitems|—|needs review|
|workitems.link.item_deleted|409|Удалённый рабочий элемент нельзя связывать.|Жойылған жұмыс элементін байланыстыруға болмайды.|workitems|—|needs review|
|workitems.link.parent_changed|409|Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.|Ата-аналық элементті басқа қатысушы өзгертіп қойған. Деректер жаңартылды — тексеріп, қайталаңыз.|workitems|—|needs review|
|workitems.link.parent_level_not_allowed|409|Родителем может быть только рабочий элемент того же или более высокого уровня.|Ата-аналық элемент тек сол деңгейдегі немесе жоғарырақ деңгейдегі жұмыс элементі бола алады.|workitems|—|needs review|
|workitems.link.parent_not_found|404|Родитель не найден в этом проекте.|Ата-аналық элемент осы жобадан табылмады.|workitems|—|needs review|
|workitems.link.self_parent|409|Рабочий элемент не может быть родителем самого себя.|Жұмыс элементі өзіне-өзі ата-аналық элемент бола алмайды.|workitems|—|needs review|
|workitems.validation_failed|400|Проверьте правильность заполнения полей.|Өрістердің дұрыс толтырылғанын тексеріңіз.|workitems|—|needs review|
|workitems.workitem.assignee_unavailable|409|Этого пользователя нельзя назначить исполнителем.|Бұл пайдаланушыны орындаушы етіп тағайындауға болмайды.|workitems|—|needs review|
|workitems.workitem.date_out_of_range|400|Укажите дату с 2000 по 2099 год.|2000 жылдан 2099 жылға дейінгі күнді көрсетіңіз.|workitems|—|needs review|
|workitems.workitem.deleted|409|Рабочий элемент удалён и доступен только для чтения.|Жұмыс элементі жойылған және тек оқуға арналған.|workitems|—|needs review|
|workitems.workitem.description_format_unknown|400|Неизвестный формат описания. Обновите страницу и попробуйте ещё раз.|Сипаттама пішімі белгісіз. Бетті жаңартып, қайталап көріңіз.|workitems|—|needs review|
|workitems.workitem.description_markup_too_large|400|Слишком много разметки. Упростите форматирование или сократите текст.|Белгілеу тым көп. Пішімдеуді жеңілдетіңіз немесе мәтінді қысқартыңыз.|workitems|—|needs review|
|workitems.workitem.description_too_long|400|Не более 100 000 символов.|100 000 таңбадан аспауы керек.|workitems|—|needs review|
|workitems.workitem.field_not_on_type|400|Это поле недоступно для рабочего элемента этого типа.|Бұл өріс осы түрдегі жұмыс элементі үшін қолжетімсіз.|workitems|—|needs review|
|workitems.workitem.integer_required|400|Введите целое число.|Бүтін сан енгізіңіз.|workitems|—|needs review|
|workitems.workitem.not_deleted|409|Рабочий элемент не удалён.|Жұмыс элементі жойылмаған.|workitems|—|needs review|
|workitems.workitem.not_found|404|Рабочий элемент не найден.|Жұмыс элементі табылмады.|workitems|—|needs review|
|workitems.workitem.number_out_of_range|400|Допустимо значение от 0 до 9 999.|0-ден 9 999-ға дейінгі мән енгізуге болады.|workitems|—|needs review|
|workitems.workitem.number_too_precise|400|Не более двух знаков после запятой.|Үтірден кейін екі таңбадан аспауы керек.|workitems|—|needs review|
|workitems.workitem.request_id_invalid|400|Не удалось отправить форму. Обновите страницу и попробуйте ещё раз.|Пішінді жіберу мүмкін болмады. Бетті жаңартып, қайталап көріңіз.|workitems|—|needs review|
|workitems.workitem.state_not_allowed|409|Этот статус недоступен для рабочего элемента этого типа.|Бұл күй осы түрдегі жұмыс элементі үшін қолжетімсіз.|workitems|—|needs review|
|workitems.workitem.target_before_start|400|Целевая дата не может быть раньше даты начала.|Мақсатты күн басталу күнінен ерте болмауы керек.|workitems|—|needs review|
|workitems.workitem.text_too_long|400|Не более 100 000 символов.|100 000 таңбадан аспауы керек.|workitems|—|needs review|
|workitems.workitem.title_multiline|400|Заголовок должен быть в одну строку.|Тақырып бір жолда болуы керек.|workitems|—|needs review|
|workitems.workitem.title_required|400|Введите заголовок.|Тақырыпты енгізіңіз.|workitems|—|needs review|
|workitems.workitem.title_too_long|400|Не более 255 символов.|255 таңбадан аспауы керек.|workitems|—|needs review|
|workitems.workitem.type_not_found|404|Тип рабочего элемента не найден.|Жұмыс элементінің түрі табылмады.|workitems|—|needs review|
|workitems.workitem.value_not_in_list|400|Выберите значение из списка.|Тізімнен мән таңдаңыз.|workitems|—|needs review|
