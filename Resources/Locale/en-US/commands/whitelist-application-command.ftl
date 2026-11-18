cmd-whitelistapply-desc = Submits a whitelist application for this server. You can only apply once.
cmd-whitelistapply-help = Usage: whitelistapply <message>
cmd-whitelistapply-message-missing = You need to write a message for your application.
cmd-whitelistapply-message-too-long = Your application is too long. The maximum is {$max} characters.
cmd-whitelistapply-created = Whitelist application #{$id} submitted. An admin will review it soon.
cmd-whitelistapply-already-applied = You have already submitted a whitelist application.
cmd-whitelistapply-failed = Your whitelist application could not be submitted.

cmd-whitelistapplications-desc = Lists every whitelist application.
cmd-whitelistapplications-help = Usage: whitelistapplications
cmd-whitelistapplications-empty = There are no whitelist applications.
cmd-whitelistapplications-entry = #{$id} | {$player} | {$status} | {$createdAt} | {$message}
cmd-whitelistapplications-total = {$count} whitelist application(s) listed.

cmd-whitelistapplicationapprove-desc = Approves a whitelist application and whitelists the player.
cmd-whitelistapplicationapprove-help = Usage: whitelistapplicationapprove <id>
cmd-whitelistapplicationapprove-approved = Approved whitelist application #{$id} for {$player}.
cmd-whitelistapplicationapprove-failed = Failed to approve whitelist application #{$id}.

cmd-whitelistapplicationdeny-desc = Denies a whitelist application.
cmd-whitelistapplicationdeny-help = Usage: whitelistapplicationdeny <id>
cmd-whitelistapplicationdeny-denied = Denied whitelist application #{$id} from {$player}.
cmd-whitelistapplicationdeny-failed = Failed to deny whitelist application #{$id}.

cmd-whitelistapplication-not-found = There is no whitelist application with id {$id}.
cmd-whitelistapplication-hint-id = [id]

cmd-whitelistapplication-status-pending = pending
cmd-whitelistapplication-status-approved = approved
cmd-whitelistapplication-status-denied = denied