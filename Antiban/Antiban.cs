using System;
using System.Collections.Generic;

namespace Antiban
{
    public class Antiban
    {       
        #region Внутренние переменные
                
        /// <summary>
        /// список сообщений
        /// </summary>
        private List<EventMessage> localList = new List<EventMessage>();

        /// <summary>
        /// 10 секунд
        /// </summary>
        private readonly TimeSpan ts10sec = new TimeSpan(0, 0, 10);

        /// <summary>
        /// 1 минута
        /// </summary>
        private readonly TimeSpan ts1m = new TimeSpan(0, 1, 0);

        /// <summary>
        /// 24 часа
        /// </summary>
        private readonly TimeSpan ts24h = new TimeSpan(24, 0, 0);

        /// <summary>
        /// Класс для запоминания последней даты для разных приоритетов в контексте добавления нового сообщения.
        /// </summary>
        private class LastDateTime
        {
            /// <summary>
            /// Последняя дата оправки по любому приоритету
            /// </summary>
            public DateTime? AnyPriority;
            /// <summary>
            /// Последняя дата оправки по первому приоритету
            /// </summary>
            public DateTime? FirstPriority;
        }

        #endregion

        #region Добавление сообщений в систему

        /// <summary>
        /// Добавление сообщений в систему, для обработки порядка сообщений
        /// </summary>
        /// <param name="eventMessage"></param>
        public void PushEventMessage(EventMessage eventMessage)
        {
            //var mes = new EventMessageExt(eventMessage);
            LastDateTime lastDt = new LastDateTime();
            for (int i = 0; i < localList.Count; i++)
            {
                UpdateLastDateTime(eventMessage, i, lastDt);
                eventMessage.DateTime = SetEstimatedDateTime(eventMessage, lastDt);
                if (CheckInsert(eventMessage, i))
                {
                    localList.Insert(i, eventMessage);
                    return;
                }
            }
            // добавляем сообщение в конец списка
            localList.Add(eventMessage);
        }

        /// <summary>
        /// Обновление последних дат по приоритетам в специальном словаре.
        /// </summary>
        /// <param name="mes">Новый элемент для вставки.</param>
        /// <param name="index">Номер индекса в списке.</param>
        /// <param name="lastDt">Класс с датами по приоритетам.</param>
        private void UpdateLastDateTime(EventMessage mes, int index, LastDateTime lastDt)
        {
            if (localList[index].Phone == mes.Phone)
            {
                lastDt.AnyPriority = localList[index].DateTime; // правило 1 минуты
                if (localList[index].Priority == 1)
                    lastDt.FirstPriority = localList[index].DateTime; // правило 24 часов 
            }
        }

        /// <summary>
        /// Устанавливаем оценочное время отправки сообщения без учета правила 10 секунд.
        /// </summary>
        /// <param name="mes">Новый элемент для вставки.</param>
        /// <param name="lastDt">Класс с датами по приоритетам.</param>
        /// <returns>Оценочное время отправки.</returns>
        private DateTime SetEstimatedDateTime(EventMessage mes, LastDateTime lastDt)
        {
            // Корректируем дату у нашего сообщения если нужно
            DateTime newDateTime = mes.DateTime;
            // Правило 1й минуты
            if (lastDt.AnyPriority.HasValue && lastDt.AnyPriority.Value.Add(ts1m) > mes.DateTime)
                newDateTime = lastDt.AnyPriority.Value.Add(ts1m);
            // Правило 24х часов 
            if (mes.Priority == 1 && lastDt.FirstPriority.HasValue && lastDt.FirstPriority.Value.Add(ts24h) > mes.DateTime)
                newDateTime = lastDt.FirstPriority.Value.Add(ts24h);

            return newDateTime;
        }

        /// <summary>
        /// Проверка возможности вставки элемента.<paramref name="mes"/>
        /// </summary>
        /// <param name="mes">Новый элемент для вставки.</param>
        /// <param name="index">Номер индекса в списке.<paramref name="eventMessages"/></param>
        /// <returns>true - можно; false - нельзя;</returns>
        private bool CheckInsert(EventMessage mes, int index)
        {
            EventMessage[] borders = GetBorders(index);
            // 1й случай: имеется только правая граница (это когда в списке только один элемент)
            if (borders[0] == null && borders[1] != null)
                if (mes.DateTime < borders[1].DateTime)
                    return true;
            // 2й случай: имеются обе границы (все остальные случаи)
            if (borders[0] != null && borders[1] != null)
                if (borders[0].DateTime < mes.DateTime && mes.DateTime < borders[1].DateTime)
                    return true;

            return false;
        }

        /// <summary>
        /// Определяем элементы между которыми предполагается вставка.
        /// </summary>
        /// <param name="index">Номер индекса в списке.</param>
        /// <returns>Массив с элементами нижней и верхней границами по времени.</returns>
        private EventMessage[] GetBorders(int index)
        {
            EventMessage[] result = new EventMessage[2];
            if (index > 0) result[0] = localList[index - 1];
            result[1] = localList[index];
            return result;
        }
        #endregion

        #region Получение сообщений на отправку
        /// <summary>
        /// Вовзращает порядок отправок сообщений
        /// </summary>
        /// <returns></returns>
        public List<AntibanResult> GetResult()
        {
            var result = new List<AntibanResult>();
            Dictionary<string, DateTime>[] dicPriority = new Dictionary<string, DateTime>[]
            {
                new Dictionary<string, DateTime>(), // [0] - Для приоритета 0
                new Dictionary<string, DateTime>()  // [1] - Для приоритета 1
            };
            for (int i = 0; i < localList.Count; i++)
            {
                // Формируем новое сообщение 
                var res = new AntibanResult() { EventMessageId = localList[i].Id, SentDateTime = localList[i].DateTime };
                // Правила отправки для 1-й минуты и 24-х часов 
                CheckAndUpdateSendDateTime(dicPriority[0], localList[i].Phone, ts1m, res);
                CheckAndUpdateSendDateTime(dicPriority[1], localList[i].Phone, ts24h, res);
                // Правило 10-и секунд
                if (i > 0 && result[i - 1].SentDateTime.Add(ts10sec) > res.SentDateTime)
                {
                    res.SentDateTime = result[i - 1].SentDateTime.Add(ts10sec);
                    // Если изменили время запоминаем его в словаре
                    if (localList[i].Priority == 0) dicPriority[0].Add(localList[i].Phone, res.SentDateTime);
                    if (localList[i].Priority == 1) dicPriority[1].Add(localList[i].Phone, res.SentDateTime);
                }
                result.Add(res);
            }
            return result;
        }

        /// <summary>
        /// Проверяем соответствует ли период между сообщениями правилам:
        /// 1) период между сообщениями на один номер, должен быть не менее 1 минуты
        /// 2) период между сообщениями с приоритетом=1 на один номер, не менее 24 часа
        /// и обновляем время отправки если нужно.
        /// </summary>
        /// <param name="dic">словарь с телефономи по определенному приоритету</param>
        /// <param name="phone">номер телефона для проверки</param>
        /// <param name="ts">период времени</param>
        /// <param name="res">отправляемое сообщение</param>
        private void CheckAndUpdateSendDateTime(Dictionary<string, DateTime> dic, string phone, TimeSpan ts, AntibanResult res)
        {
            if (dic.ContainsKey(phone))
            {
                // период между сообщениями на один номер, должен быть не менее ts
                if (dic[phone].Add(ts) > res.SentDateTime)
                {
                    dic[phone] = dic[phone].Add(ts);
                    res.SentDateTime = dic[phone];
                }
            }
        }
        #endregion
    }
}
