import { describe, expect, it } from 'vitest';
import { buildLanguageByCodeQuery, buildLanguagesQuery } from './dictionariesApi';
import { ConditionalOperator } from '../../../shared/api/paging';

describe('buildLanguagesQuery', () => {
  it('с полностью дефолтным фильтром отправляет только page=1&pageSize=10', () => {
    expect(buildLanguagesQuery()).toBe('page=1&pageSize=10');
    expect(buildLanguagesQuery({})).toBe('page=1&pageSize=10');
  });

  it('никогда не отправляет пустой pageSize/page', () => {
    // Даже при явном обнулении фильтров page/pageSize остаются конкретными
    // значениями, никогда `?pageSize=` (пустой pageSize — это 400, а не
    // молчаливый дефолт).
    const query = buildLanguagesQuery({ code: '', endonym: undefined, page: 3, pageSize: 25 });
    expect(query).toBe('page=3&pageSize=25');
    expect(query).not.toContain('pageSize=&');
    expect(query.endsWith('pageSize=')).toBe(false);
  });

  it('каждое "пустое" значение (\'\', undefined) опускается, а не отправляется вхолостую', () => {
    const query = buildLanguagesQuery({
      code: '',
      endonym: undefined,
      isDefault: undefined,
      isActive: undefined,
      sortOrder: '',
    });
    expect(query).toBe('page=1&pageSize=10');
  });

  it('строит filter/orderBy только по белому списку полей', () => {
    const query = buildLanguagesQuery({
      code: 'k',
      endonym: 'Қаз',
      isDefault: 'false',
      isActive: 'true',
      sortOrder: '20',
      orderBy: { field: 'endonym', desc: true },
    });
    const params = new URLSearchParams(query);
    expect(params.get('orderBy')).toBe('endonym desc');
    // `=*` уже сама по себе оператор Contains — значение после неё не
    // оборачивается в `*...*`, иначе Gridify ищет литеральную звёздочку в
    // значении и не находит ничего ни на каком реальном сервере.
    expect(params.get('filter')).toBe('code=*k,endonym=*Қаз,isDefault=false,isActive=true,sortOrder=20');
    expect(params.get('page')).toBe('1');
    expect(params.get('pageSize')).toBe('10');
  });

  it('orderBy без desc отправляет только имя поля', () => {
    const query = buildLanguagesQuery({ orderBy: { field: 'code' } });
    expect(new URLSearchParams(query).get('orderBy')).toBe('code');
  });

  // Раньше `filter` собирался конкатенацией строк без экранирования —
  // значение с `,`, `|`, `(`, `)` или буквальным `/i` меняло структуру
  // выражения (лишнее условие, незакрытая группа) и сервер отвечал 400 вместо
  // обычного «Ничего не найдено». `GridifyQueryBuilder.addCondition`
  // экранирует это сам (`escapeValue` по умолчанию `true`) — тест пинит и сам
  // факт экранирования, и то, что построение запроса с такими символами не
  // бросает исключение.
  it('значение фильтра с `,`, `|`, `(`, `)` и `/i` экранируется, а не ломает filter', () => {
    const dangerous = 'a,b|(c)/i';
    let query = '';
    expect(() => {
      query = buildLanguagesQuery({ code: dangerous });
    }).not.toThrow();
    const filter = new URLSearchParams(query).get('filter');
    expect(filter).toBe('code=*a\\,b\\|\\(c\\)\\/i');
    // Экранированные спецсимволы больше не читаются как логический оператор
    // или разделитель группы — это литеральная часть значения.
    expect(filter).not.toBe(`code=*${dangerous}`);
  });

  // Словарь операторов, проверенный на настоящем Gridify-парсере бэкенда:
  //   code=ru    -> 1 строка: `=` — это Equal целиком, значение идёт сразу
  //                 после него;
  //   code=*r    -> 1 строка: `=*` — это уже сам по себе оператор Contains;
  //   code=*r*   -> 0 строк, без исключения: замыкающая `*` — литеральный
  //                 символ значения, а не wildcard "конец строки неважен".
  // Отсюда: билдер обязан оборачивать значение в `=*<value>`, никогда в
  // `=*<value>*`. Это пиновка словаря операторов пакета (не сети — здесь
  // нет живого Gridify), а не вывод из собственной документации к билдеру.
  it('Contains — это сам оператор `=*`, значение после него не оборачивается в `*…*`', () => {
    expect(ConditionalOperator.Contains).toBe('=*');
    const filter = new URLSearchParams(buildLanguagesQuery({ code: 'r' })).get('filter');
    expect(filter).toBe('code=*r');
    expect(filter).not.toBe('code=*r*');
  });
});

describe('buildLanguageByCodeQuery', () => {
  // `code==ru` — `GridifyFilteringException: Invalid expression format
  // 'ValueExpression'` на реальном парсере, где `code=ru` -> 1 строка.
  // Равенство в Gridify — это одиночный `=`, не `==`.
  it('строит filter=code=<code>&page=1&pageSize=1 (нет GET /languages/{code})', () => {
    expect(ConditionalOperator.Equal).toBe('=');
    const query = buildLanguageByCodeQuery('ru');
    const params = new URLSearchParams(query);
    expect(params.get('filter')).toBe('code=ru');
    expect(params.get('filter')).not.toContain('==');
    expect(params.get('page')).toBe('1');
    expect(params.get('pageSize')).toBe('1');
  });
});
