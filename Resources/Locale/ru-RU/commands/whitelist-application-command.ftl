cmd-whitelistapply-desc = Отправляет заявку в вайтлист сервера. Заявку можно отправить только один раз.
cmd-whitelistapply-help = Использование: whitelistapply <сообщение>
cmd-whitelistapply-message-missing = Вам нужно написать сообщение для заявки.
cmd-whitelistapply-message-too-long = Заявка слишком длинная. Максимум: { $max } символов.
cmd-whitelistapply-created = Заявка в вайтлист #{ $id } отправлена. Администратор скоро её рассмотрит.
cmd-whitelistapply-already-applied = Вы уже отправили заявку в вайтлист.
cmd-whitelistapply-failed = Не удалось отправить заявку в вайтлист.

cmd-whitelistapplications-desc = Показывает все заявки в вайтлист.
cmd-whitelistapplications-help = Использование: whitelistapplications
cmd-whitelistapplications-empty = Заявок в вайтлист нет.
cmd-whitelistapplications-entry = #{ $id } | { $player } | { $status } | { $createdAt } | { $message }
cmd-whitelistapplications-total = Показано заявок: { $count }.

cmd-whitelistapplicationapprove-desc = Одобряет заявку в вайтлист и добавляет игрока в вайтлист.
cmd-whitelistapplicationapprove-help = Использование: whitelistapplicationapprove <id>
cmd-whitelistapplicationapprove-approved = Заявка #{ $id } от { $player } одобрена.
cmd-whitelistapplicationapprove-failed = Не удалось одобрить заявку #{ $id }.

cmd-whitelistapplicationdeny-desc = Отклоняет заявку в вайтлист.
cmd-whitelistapplicationdeny-help = Использование: whitelistapplicationdeny <id>
cmd-whitelistapplicationdeny-denied = Заявка #{ $id } от { $player } отклонена.
cmd-whitelistapplicationdeny-failed = Не удалось отклонить заявку #{ $id }.

cmd-whitelistapplication-not-found = Заявки в вайтлист с id { $id } не существует.
cmd-whitelistapplication-hint-id = [id]

cmd-whitelistapplication-status-pending = ожидает
cmd-whitelistapplication-status-approved = одобрена
cmd-whitelistapplication-status-denied = отклонена