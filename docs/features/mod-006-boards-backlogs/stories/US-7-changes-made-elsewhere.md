# US-7: Changes made elsewhere move items on and off a team's surfaces

- story: US-7
- depends-on: US-1, US-4
- shippable: no — it states how US-1's backlog and US-4's board follow other modules' changes
- status: pending

As a **signed-in user**, I want **an item to show on the team that owns its
area, wherever its area or state was changed**, so that **my backlog and my
board never show another team's work or a stale state for long**.

## Acceptance criteria

Data: US-1's. The «Истории» backlog of «Мастаск», reordered, reads MSK-US-5,
MSK-US-7. Every scenario but the board one belongs to pass A. Spec § Model,
BR-M4-5, -6, -20, -24, -28; mod-002 BR-M1-11, BR-M1-18.

```gherkin
Scenario: A new Область moves the item to the other team's backlog
  Given MSK-US-3 on the «Истории» backlog of «Платформа»
  When its Область is set to «Отчёты» on its form
  Then on the next read it is not on the backlog of «Платформа»
  And the backlog of «Мастаск» reads MSK-US-5, MSK-US-7, MSK-US-3

Scenario: An area given another owner takes its items along
  Given MSK-US-3 and MSK-BUG-4 in «Платформа / API»
  When «Мастаск» is made the owner of «Платформа / API» in «Области»
  Then both leave the backlog of «Платформа»
  And the backlog of «Мастаск» reads MSK-US-5, MSK-US-7, MSK-US-3, MSK-BUG-4

Scenario: An area moved under another parent takes its items along
  Given MSK-US-3 in «Платформа / API», which has no owner of its own
  When «Платформа / API» is moved under the root «Мастаск»
  Then MSK-US-3 is the last row of the «Истории» backlog of «Мастаск»

Scenario: An item that comes back before anything was ordered returns
  Given the backlog of «Платформа» reordered to MSK-US-9, MSK-US-3, MSK-US-11
  When MSK-US-9's Область is set to «Отчёты» and back to «Платформа», with no
    reorder of either backlog in between
  Then MSK-US-9 is first on the backlog of «Платформа» again

Scenario: An item placed elsewhere meanwhile comes back last
  Given the backlog of «Платформа» reordered to MSK-US-9, MSK-US-3, MSK-US-11
  When MSK-US-9's Область is set to «Отчёты», the backlog of «Мастаск» is
    reordered, and MSK-US-9's Область is set back to «Платформа»
  Then the backlog of «Платформа» reads MSK-US-3, MSK-US-11, MSK-US-9

Scenario: A deleted team's items go to the first team, in creation order
  Given team «Аналитика», whose «Истории» backlog is reordered to MSK-US-30,
    MSK-US-21
  When «Аналитика» is deleted
  Then its backlog and board addresses answer «Команда не найдена», and the
    switcher no longer lists it
  And the backlog of «Мастаск» reads MSK-US-5, MSK-US-7, MSK-US-21, MSK-US-30

Scenario: An item created from the project's list goes to the first team
  When a «Баг» is created from «Рабочие элементы», which uses the root area
  Then it is the last row of the «Истории» backlog of «Мастаск»

Scenario: A state set on the form moves the card (pass B)
  Given MSK-US-9 in «Новая» on the «Истории» board of «Платформа», «Порядок» 1
  When its «Статус» is set to «Разрешена» on its form
  Then on the next read its card is in the column «Разрешена»
  And its backlog row shows «Разрешена» with «Порядок» 1

Scenario: Nothing is pushed to an open page
  Given Анна has the backlog of «Платформа» open
  When Борис moves MSK-US-11 to the top
  Then Анна's page is unchanged and shows no notice
  And it shows Борис's order once it is read again: after Анна's own next
    move, on a reload, or when she returns to the browser tab
```
