using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using TBL = rw_cos_mei.AppTable;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///Activity_Init
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme.Splash", NoHistory = true)]
    public class Activity_Init : AppCompatActivity
    {

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        //#############################################################################

        protected override void OnResume()
        {
            base.OnResume();

            //Statischen Speicher erstellen
            InitRoutine(this);

            //Job erstellen
            JobSchedulerHelper.CreateSyncJob(this, TBL.GetSyncIntervalSettingDescriptor(this, TBL.SyncInterval).Timespan);

            //App starten
            StartActivity(new Intent(Application.Context, typeof(Activity_Main)));

        }

        public static void InitRoutine(Context context)
        {

            //Statischen App-Speicher vorbereiten
            TBL.BlockSyncService();         //BackgroundSync sperren

            TBL.Init(context);              //AppTable initialisieren (AppTable = TaBLe)
            TBL.DB_Object.LoadDatabase();   //Feed aus der lokalen Datenbank laden

            TBL.UnBlockSyncService();       //BackgroundSync entsperren

        }

        //#############################################################################

        public override void OnBackPressed()
        {
            //Nichts tun, damit der Ladebildschirm nicht im Stack landet.
        }

    }

}