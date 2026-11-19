namespace Content.Shared.Database
{
    /*
     * Editing the numbers here may obliterate DB records, you have been warned.
     * If you do have to edit the numbers for some reason, please create migrations.
     * Adding new types is fine (or even renaming), but do not remove or change them.
     */
    /// <summary>
    ///     Review state of a whitelist application.
    /// </summary>
    public enum WhitelistApplicationStatus
    {
        /// <summary>
        ///     Submitted and waiting for an admin to review it.
        /// </summary>
        Pending = 0,

        /// <summary>
        ///     Accepted. The applicant was added to the whitelist.
        /// </summary>
        Approved = 1,

        /// <summary>
        ///     Rejected. Does not remove an existing whitelist entry.
        /// </summary>
        Denied = 2,
    }
}
