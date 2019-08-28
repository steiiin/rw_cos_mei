using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;

using System.Threading.Tasks;

using TBL = rw_cos_mei.AppTable;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///SyncService
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    [Service(Name = "com.steiiin.rw_cos_mei.SyncService", Exported = true, Permission = "android.permission.BIND_JOB_SERVICE")]
    public class SyncService : JobService
    {

        public override bool OnStartJob(JobParameters @params)
        {
            
            if(TBL.IsSyncBlocked) { JobFinished(@params, false); return false; }

            Task.Run(async delegate
            {

                //Statische Klassen erstellen
                TBL.Init(this);
                TBL.DB_Object.LoadDatabase();

                //Feed aktualiseren
                await TBL.SP_Object.UpdateNewsFeed(true, true);
                
                //Job schließen
                JobFinished(@params, false);

            });

            return true;
        }
        public override bool OnStopJob(JobParameters @params)
        {
            if (TBL.DB_Object != null) { TBL.DB_Object.Close(); }
            return true;
        }

    }

    public static class JobSchedulerHelper
    {

        public const int JOB_ID = 9991;

        //####################################################################################################

        public static JobInfo.Builder CreateJobBuilderUsingJobId<T>(this Context context, int jobId) where T : JobService
        {
            var javaClass = Java.Lang.Class.FromType(typeof(T));
            var comName = new ComponentName(context, javaClass);
            return new JobInfo.Builder(jobId, comName);
        }

        public static void CreateSyncJob(Context context, int syncInterval)
        {

            var jobBuilder = JobSchedulerHelper.CreateJobBuilderUsingJobId<SyncService>(context, JobSchedulerHelper.JOB_ID);

            if (Build.VERSION.SdkInt < BuildVersionCodes.N)
            {
                jobBuilder.SetPeriodic(syncInterval);
            }
            else
            {
                jobBuilder.SetPeriodic(syncInterval, 300000);
            }
                
            jobBuilder.SetPersisted(true);
            jobBuilder.SetRequiredNetworkType(NetworkType.Any);
            jobBuilder.SetBackoffCriteria(60000, BackoffPolicy.Exponential);
            var jobInfo = jobBuilder.Build();

            var jobScheduler = (JobScheduler)context.GetSystemService(Activity.JobSchedulerService);
            var scheduleResult = jobScheduler.Schedule(jobInfo);

        }

    }

}