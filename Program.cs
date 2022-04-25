using System.Runtime.CompilerServices;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;

int userCount = 100000; // Change to modify number of users
int articleCount = 500;
int videoCount = 1000;
int totalDataSetMonths = 24; // Change to extend data set
var initalUsers = new ProbabilityRange(0.3, 0.4);
var existingUserSubbed = new ProbabilityRange(0.8, 0.9);
var newUserJoinPR = new ProbabilityRange(-0.1, 0.4);

int initialUsers = P.Dv(userCount, initalUsers);
int usersToJoin = userCount - initialUsers;

DateOnly finalMonth = DateOnly.FromDateTime(DateTime.Now);
finalMonth = finalMonth.AddDays(-finalMonth.Day + 1);
DateOnly initialMonth = finalMonth.AddMonths(-totalDataSetMonths);

Dump(userCount);
Line();
Dump(initialUsers);
Dump(usersToJoin);
Line();
Dump(totalDataSetMonths);
Dump(initialMonth);
Dump(finalMonth);

Stopwatch tsw = Stopwatch.StartNew();

// Generate users
User[] users = new User[userCount];
for(long i=0; i<userCount; i++) users[i] = User.NewUser();

// Generate media
Guid[] articles = new Guid[articleCount];
for(long i=0; i<articleCount; i++) articles[i] = Guid.NewGuid();
Guid[] videos = new Guid[videoCount];
for(long i=0; i<videoCount; i++) videos[i] = Guid.NewGuid();

// Build pools
List<User> existingUserPool = users.Take(initialUsers).ToList();
List<User> newUserPool = users.Skip(initialUsers).ToList();

// Set pre-subscribed
foreach (var u in existingUserPool)
    if (P.Db(existingUserSubbed)) u.PreSubUpdate();

// Build new user ramp (random linear)
int[] newUserRamp = GenerateLinearRandomRamp(totalDataSetMonths, usersToJoin, newUserJoinPR);
Line();
DumpIA(newUserRamp);

Stopwatch gsw = Stopwatch.StartNew();
List<Event> events = new List<Event>();
List<Subscription> subs = new List<Subscription>();
// Loop though each month
for(int i=0; i< totalDataSetMonths; i++)
{
    Stopwatch sw = Stopwatch.StartNew();
    long eventsAdded = 0;
    long subsAdded = 0;
    DateOnly thisMonth = initialMonth.AddMonths(i);
    Line();
    Console.WriteLine($">>> Generating {thisMonth}");

    // Add new users
    existingUserPool.AddRange(newUserPool.Take(newUserRamp[i]));
    newUserPool.RemoveRange(0, newUserRamp[i]);
    Console.WriteLine($"Added {newUserRamp[i]} new users");

    // Generate activity
    foreach (User u in existingUserPool)
    {
        var ev = u.GenerateEvents(thisMonth, articles, videos);
        eventsAdded += ev.Count;
        events.AddRange(ev);
        if (u.TryGenerateSubscription(thisMonth, out Subscription? s) && s != null)
        {
            subs.Add(s);
            subsAdded++;
        }
    }
    sw.Stop();
    Console.WriteLine($"Added {eventsAdded} events");
    Console.WriteLine($"Added {subsAdded} subs");
    Console.WriteLine($"In {sw.ElapsedMilliseconds/1000.0:0.00}s, estimated {((gsw.ElapsedMilliseconds/1000.0)/(i+1.0))*(totalDataSetMonths-(i+1.0)):0.00} remaining");
}

tsw.Stop();

Console.WriteLine($"\n\nFinal Stats:");
Console.WriteLine($"User Pool: {existingUserPool.Count}, Left Over New Users: {newUserPool.Count}");
Console.WriteLine($"Total Events: {events.Count}");
Console.WriteLine(string.Join("\n", events.GroupBy(x=>x.EventType).Select(x=>$"   {x.Key}: {x.Count()}")));
Console.WriteLine($"Total Subscriptions: {subs.Count}");
Line();
Console.WriteLine($"Generated in {tsw.ElapsedMilliseconds/1000.0:0.00}s");

Line();
Console.WriteLine("Writing to disk...");
tsw.Restart();
File.WriteAllText("users.json", JsonSerializer.Serialize(users));
File.WriteAllText("events.json", JsonSerializer.Serialize(events));
File.WriteAllText("subs.json", JsonSerializer.Serialize(subs));
tsw.Stop();
Console.WriteLine($"Saved in {tsw.ElapsedMilliseconds/1000.0:0.00}s");

Console.WriteLine($"\n\nDistribution Stats:");


// Dump 100 users
//for(int i=0; i<100; i++) Console.WriteLine(users[i]);

int[] GenerateLinearRandomRamp(int steps, int totalSum, ProbabilityRange range)
{
    double[] ramp = new double[steps];
    ramp[0] = 1.0;
    double rampSum = ramp[0];
    for(int i=1; i<steps; i++)
    {
        ramp[i] = ramp[i-1] + P.D(range);
        rampSum += ramp[i];
    }
    int[] itemRamp = new int[steps];
    int itemRampSum = 0;
    for(int i=0; i<steps-1; i++)
    {
        itemRamp[i] = (int)(totalSum * (ramp[i] / rampSum));
        itemRampSum += itemRamp[i];
    }
    itemRamp[steps-1] = totalSum - itemRampSum;
    return itemRamp;
}

void Dump(object o, [CallerArgumentExpression("o")] string name="Unknwon") => Console.WriteLine($"{name}: {o}");
void DumpDA(double[] o, [CallerArgumentExpression("o")] string name="Unknwon") => Console.WriteLine($"{name}: [ {string.Join(", ", o)} ]");
void DumpIA(int[] o, [CallerArgumentExpression("o")] string name="Unknwon") => Console.WriteLine($"{name}: [ {string.Join(", ", o)} ]");
void Line() => Console.WriteLine();
struct ProbabilityRange
{
    public double Min {get;}
    public double Max {get;}
    public ProbabilityRange(double min, double max)
    {
        Min = min;
        Max = max;
    }
}
class P
{
    private static readonly Random _r = new Random();
    public static double D(double min, double max) => (max - min) * D() + min;
    public static double D(ProbabilityRange range) => D(range.Min, range.Max);
    public static double D() => _r.NextDouble();
    public static long Dv(long value, double min, double max) => (long)(value * D(min, max));
    public static int Dv(int value, double min, double max) => (int)(value * D(min, max));
    public static int Dv(int value, ProbabilityRange range) => (int)(value * D(range));
    public static int Dv(ProbabilityRange range) => (int)D(range);
    public static bool Db(ProbabilityRange range) => D() <= D(range);
    public static bool B(double threshold) => D() <= threshold;
    public static int I(int min, int max) => (int)((max - min) * D() + min);
    public static T Ti<T>(params T[] values) => values[I(0, values.Length-1)];
}
record User(Guid UserId, string FirstName, string LastName)
{
    private static readonly NameGenerator _nameGen = new NameGenerator();
    private static readonly Calendar _cal = new GregorianCalendar(); 
    private bool _subbed = false;
    private bool _unhappy = false;
    private int _unhappyDelay = 0;
    private int _unsubDelay = 0;
    private int _presubDelay = 0;
    

    private double _loginMod = 0.0;
    private int _maxLogin = 3;
    private ProbabilityRange _video = new ProbabilityRange();
    private ProbabilityRange _article = new ProbabilityRange();
    private ProbabilityRange _share = new ProbabilityRange();
    private double _skipDayMod = 0.0;

    public void PreCalc()
    {
        _skipDayMod = P.D(0.95, 1);
        _loginMod = P.D(0.15, 1);
        _video = new ProbabilityRange(P.I(0,2), P.I(3,5));
        _article = new ProbabilityRange(P.I(0,1), P.I(1,3));
        _share = new ProbabilityRange(0.0, (_video.Max + _article.Max)/40.0);
        _unsubDelay = P.Ti(0, 1, 1, 1, 2, 2);
        _presubDelay = P.Ti(0, 0, 1, 1, 1);
        if(P.B(0.005)) _presubDelay = int.MaxValue;
        _unhappyDelay = P.Ti(0, 1, 1, 1, 1, 2, 2, 2, 3) switch {
            0 => P.I(1,2),
            2 => P.I(6,12),
            3 => int.MaxValue,
            _ => P.I(3,6)
        };
    }

    public static User NewUser()
    {
        (string first, string last) = _nameGen.RandomName();
        var user = new User(Guid.NewGuid(), first, last);
        user.PreCalc();
        return user;
    }
    public bool TryGenerateSubscription(DateOnly month, out Subscription? sub)
    {
        sub = null;
        if (!_subbed && _presubDelay <= 0) _subbed = true;
        if (!_subbed && _presubDelay > 0) _presubDelay--;
        if (!_subbed) return false;
        if (!_unhappy && _unhappyDelay <= 0) _unhappy = true;
        if (!_unhappy && _unhappyDelay > 0) _unhappyDelay--;
        if (_unhappy && _unsubDelay <= 0) _subbed = false;
        if (_unhappy) _unsubDelay--;
        sub = new Subscription(month.ToDateTime(new TimeOnly(0, 0)), this.UserId, !_subbed);
        return true;
    }
    public List<Event> GenerateEvents(DateOnly month, Guid[] articles, Guid[] videos)
    {
        int daysInMonth = _cal.GetDaysInMonth(month.Year, month.Month);

        // generate events
        List<Event> events = new();
        for(int i=0; i<daysInMonth; i++)
        {
            DateTime start = month.AddDays(i).ToDateTime(new TimeOnly(P.I(6, 15), P.I(0, 59)));
            events.AddRange(GenerateDay(start, articles, videos));
        }
        return events;
    }
    private int HappyModify(int value)
    {
        if (_subbed && !_unhappy) return value * 2;
        if (_subbed || !_unhappy) return value;
        return value / 2;
    }
    private List<Event> GenerateDay(DateTime start, Guid[] articles, Guid[] videos)
    {
        List<Event> events = new List<Event>();
        if (P.B(_skipDayMod)) return events;
        for(int j=0; j<_maxLogin; j++)
        {
            events.Add(new Event(start, EventType.LogInEvent, this.UserId, null));

            List<EventData> toAdd = new List<EventData>();
            int videosToAdd = HappyModify(P.Dv(_video));
            for(int i=0; i<videosToAdd; i++) toAdd.Add(new EventData(ObjectType.Video, P.Ti(videos)));
            int articlesToAdd = HappyModify(P.Dv(_article));
            for(int i=0; i<articlesToAdd; i++) toAdd.Add(new EventData(ObjectType.Article, P.Ti(articles)));
            foreach(EventData e in toAdd.OrderBy(x=>P.D()))
            {
                start = start.AddSeconds(P.I(30, 5*60));
                events.Add(new Event(start, e.ObjectType == ObjectType.Video ? EventType.VideoWatch : EventType.ArticleRead, this.UserId, e));
                if (P.Db(_share))
                {
                    start = start.AddSeconds(P.I(60, 180));
                    events.Add(new Event(start, EventType.SocialShare, this.UserId, e));
                }
            }
            if (!P.B(_loginMod)) break;
            start = start.AddMinutes(P.I(60, 180));
        }
        return events;
    }

    public void PreSubUpdate() => _subbed = true; 
}
record Subscription(DateTime SubscriptionDate, Guid UserId, bool Canceled)
{
}
record Event(DateTime EventDate, EventType EventType, Guid UserId, EventData? EventData)
{
}
record EventData(ObjectType ObjectType, Guid ObjectId)
{
}
enum EventType
{
    LogInEvent,
    ArticleRead,
    VideoWatch,
    SocialShare
}
enum ObjectType
{
    Article,
    Video
}
class NameGenerator
{
    private static readonly Random _r = new Random();
    private static readonly string[] _files = new [] { "names1.txt", "names2.txt" };
    private string[][] _names;
    public NameGenerator()
    {
        _names = new string[_files.Length][];
        for(int i=0; i<_files.Length; i++) _names[i] = File.ReadAllLines(_files[i]);
    }
    public (string FirstName, string LastName) RandomName()
    {
        int first = _r.Next(_names.Length);
        return (RandomName(first), RandomName((first + 1) % _names.Length));
    }
    private string RandomName(int index) => _names[index][_r.Next(_names[index].Length)];
}