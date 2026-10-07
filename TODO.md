# TODO

Выполненные задачи отмечать [x] и переносить в DONE.md

## GUI

- [x] Выбор устройства/потока Icecast или HLS и управление анализом Старт/Стоп.
- [x] Peak rail с True Peak, независимыми кликами сброса MAX и Overload.
- [x] Стерео Waterfall L/R с синхронным курсором, частотными подписями,
  шкалой 20 Hz–20 kHz и десятисекундным окном.
- [x] Loudness rolling history для Momentary/Short-term/Integrated, крупное
  LUFS-значение и настраиваемые окно/масштаб.
- [x] RTA с разрешением 1/1, 1/3, 1/6, 1/12 октавы и выбором Mono/L/R.
- [x] Квадратный Phase Scope и настройка компенсации gain.
- [x] Узкий peak rail и полноразмерные вкладки Waterfall, RTA, Loudness и Phase
  с единообразными действиями Reset/Screenshot/Settings.
- [x] Настройки всех шести страниц сохраняются между запусками; добавлены
  конфигурационные страницы по предоставленным reference screenshots.
- [x] Выравнивание Waterfall, Loudness, RTA и Peak rail по reference
  screenshots (см. CHANGELOG 0.038).
- [ ] Ручная приёмка на Windows: проверить реальный звук и устройства, клики
  MAX/Overload, ресайз, Apply/Cancel после перезапуска, PNG каждой вкладки,
  и запуск GUI на целевой машине.
  
  Дополнительно сверить с референсами визуально (скриншот окна в автоматической
  проверке не получился): Peak rail, Waterfall L/R сверху вниз, Loudness-тулбар,
  RTA-оси и сегменты, кнопка Rolling (циклит окно Integrated).
