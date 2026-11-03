using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // Whether schtasks' list of every task holds one, read from its CSV, whose task names no Windows language changes.
    public class UserTasksTests
    {
        private const string List =
            "\"\\Adobe Acrobat Update Task\",\"N/A\",\"Prêt\"\r\n" +
            "\"\\XboxSeries.BootShell.SafetyNet\",\"N/A\",\"Prêt\"\r\n" +
            "\"\\Microsoft\\Windows\\Defrag\\ScheduledDefrag\",\"N/A\",\"Prêt\"\r\n";

        [Fact]
        public void A_task_in_the_root_folder_is_found_whatever_the_language()
        {
            Assert.True(UserTasks.Lists(List, "XboxSeries.BootShell.SafetyNet"));
        }

        [Fact]
        public void A_task_not_listed_isnt_found()
        {
            Assert.False(UserTasks.Lists(List, "XboxSeries.BootShell.Other"));
        }

        [Fact]
        public void A_task_of_the_same_name_in_another_folder_or_a_longer_name_isnt_it()
        {
            Assert.False(UserTasks.Lists(List, "ScheduledDefrag"));
            Assert.False(UserTasks.Lists(List, "XboxSeries.BootShell"));
        }

        [Fact]
        public void The_name_is_matched_without_regard_to_case_as_Task_Scheduler_does()
        {
            Assert.True(UserTasks.Lists(List, "xboxseries.bootshell.safetynet"));
        }
    }
}
