using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace wsjtx_all_scanner
{
    class Program
    {
        static void Main(string[] args)
        {
            List<QsoFragment> fragments = new List<QsoFragment>();

            using (var fs = File.Open(@"C:\Users\tomandels\AppData\Local\WSJT-X\ALL.TXT", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                DateTime dt = default(DateTime);
                string mode;
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        break;

                    if (Regex.IsMatch(line, "^\\d{4}-((0[1-9])|(1[012]))-((0[1-9]|[12]\\d)|3[01]) ([0-1][0-9]|[2][0-3]):([0-5][0-9])$*"))
                    {
                        dt = new DateTime(
                            int.Parse(line.Substring(0, 4)),
                            int.Parse(line.Substring(5, 2)),
                            int.Parse(line.Substring(8, 2)),
                            int.Parse(line.Substring(11, 2)),
                            int.Parse(line.Substring(14, 2)),
                            0);

                        mode = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[4];
                    }
                    // 170718_215915
                    else if (Regex.IsMatch(line, "^\\d{6}_\\d{6}$*"))
                    {
                        dt = new DateTime(
                            2000 + int.Parse(line.Substring(0, 2)),
                            int.Parse(line.Substring(2, 2)),
                            int.Parse(line.Substring(4, 2)),
                            int.Parse(line.Substring(7, 2)),
                            int.Parse(line.Substring(9, 2)),
                            int.Parse(line.Substring(11, 2)));
                    }
                    else if (Regex.IsMatch(line, "^\\d{6}$*"))
                    {
                        int hr, mn, sc;
                        hr = int.Parse(line.Substring(0, 2));
                        mn = int.Parse(line.Substring(2, 2));
                        sc = int.Parse(line.Substring(4, 2));
                        dt = new DateTime(dt.Year, dt.Month, dt.Day, hr, mn, sc);
                    }

                    if (dt == default(DateTime))
                    {
                        continue;
                    }

                    string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        string to = fields.LastBut(2);
                        string from = fields.LastBut(1);
                        string content = fields.Last();

                        var fragment = new QsoFragment();
                        fragment.DateTime = dt;
                        fragment.LogLine = line;

                        if (fields.Length >= 8)
                        {
                            if (fields[1] != "Transmitting")
                            {
                                int hearddb = int.Parse(fields[1]);
                                int hz = int.Parse(fields[3]);
                            }

                            if (to == "CQ")
                            {
                                fragment.Part = ExchangePart.Call1_cq;
                                fragment.CallAGrid = content;
                                fragment.CallA = from;
                            }
                            else if (content != "RR73" && content.Length == 4 && char.IsLetter(content[0]) && char.IsLetter(content[1]) && char.IsNumber(content[2]) && char.IsNumber(content[3]))
                            {
                                fragment.Part = ExchangePart.Reply1_grid;
                                fragment.CallBGrid = content;
                                fragment.CallA = to;
                                fragment.CallB = from;
                            }
                            else if (content[0] == '+' || content[0] == '-')
                            {
                                fragment.Part = ExchangePart.Call2_rpt;
                                if (!int.TryParse(content.Substring(1), out int rpt))
                                {
                                    continue;
                                }
                                fragment.Report = rpt;
                                if (content[0] == '-')
                                {
                                    fragment.Report *= -1;
                                }

                                fragment.CallA = from;
                                fragment.CallB = to;
                            }
                            else if (content.StartsWith("R") && content.Length >= 3 && (content[1] == '+' || content[1] == '-'))
                            {
                                fragment.Part = ExchangePart.Reply2_rpt;

                                if (!int.TryParse(content.Substring(2), out int rpt))
                                {
                                    continue;
                                }
                                fragment.ReplyReport = rpt;

                                if (content[1] == '-')
                                {
                                    fragment.ReplyReport *= -1;
                                }

                                fragment.CallA = to;
                                fragment.CallB = from;
                            }
                            else if (content == "RR73" || content == "RRR")
                            {
                                fragment.Part = ExchangePart.Ack;

                                fragment.CallA = from;
                                fragment.CallB = to;
                            }

                            fragments.Add(fragment);
                        }
                    }
                    /*catch (Exception ex)
                    {
                        Console.WriteLine(line);
                        for (int i = 0; i < fields.Length; i++)
                        {
                            Console.WriteLine($"  {i}: {fields[i]}");
                        }
                        Console.WriteLine();
                        Debugger.Break();
                    }*/
                    finally { }
                }
            }

            var myQsoFragments = fragments.Where(f => f.CallA == "M0LTE" || f.CallB == "M0LTE").ToList();

#warning This bit isn't quite there yet.
            var qsos = new List<Qso>();
            int lastpc = 0;
            int cnt = myQsoFragments.Count;
            for (int i =0; i< cnt;i++)
            {
                int pc = i * 100 / cnt;
                if (pc != lastpc)
                {
                    Console.WriteLine(pc);
                    lastpc = pc;
                }

                var item = myQsoFragments[i];

                var matches = qsos.Where(q => q.Day == item.DateTime.Date && q.CallA == item.CallA && q.CallB == item.CallB && !q.Complete);

                Qso qso;
                if (!matches.Any())
                {
                    qso = new Qso();
                    qsos.Add(qso);
                }
                else
                {
                    qso = matches.Last();
                }

                if (item.Part == ExchangePart.Call1_cq)
                {
                    qso.Call1 = item;
                }
                else if (item.Part == ExchangePart.Reply1_grid)
                {
                    qso.Reply1 = item;
                }
                else if (item.Part == ExchangePart.Call2_rpt)
                {
                    qso.Call2 = item;
                }
                else if (item.Part == ExchangePart.Reply2_rpt)
                {
                    qso.Reply2 = item;
                }
                else if (item.Part == ExchangePart.Ack)
                {
                    qso.Ack = item;
                }
            }

            Debugger.Break();

        }
    }

    enum ExchangePart
    {
        Call1_cq, Reply1_grid, Call2_rpt, Reply2_rpt, Ack
    }

    class Qso
    {
        public string CallA { get { return Call1 != null ? Call1.CallA : Reply1 != null ? Reply1.CallA : Call2 != null ? Call2.CallA : Reply2 != null ? Reply2.CallA : Ack?.CallA; } }
        public string CallB { get { return Call1 != null ? Call1.CallB : Reply1 != null ? Reply1.CallB : Call2 != null ? Call2.CallB : Reply2 != null ? Reply2.CallB : Ack?.CallB; } }
        public DateTime Day
        {
            get
            {
                var dt = Call1 != null ? Call1.DateTime : Reply1 != null ? Reply1.DateTime : Call2 != null ? Call2.DateTime : Reply2 != null ? Reply2.DateTime : Ack?.DateTime;
                return dt.Value.Date;
            }
        }
        public QsoFragment Call1 { get; set; }
        public QsoFragment Reply1 { get; set; }
        public QsoFragment Call2 { get; set; }
        public QsoFragment Reply2 { get; set; }
        public QsoFragment Ack { get; set; }

        public bool Complete { get { return Call1 != null && Call2 != null && Reply1 != null && Reply2 != null && Ack != null; } }
    }

    [DebuggerDisplay("{Part}: {LogLine}")]
    class QsoFragment
    {
        // CQ a grid
        //  a b grid
        //  b a db
        //  a b R+db
        //  b a RRR
        public ExchangePart Part { get; internal set; }
        public int? Report { get; internal set; }
        public int? ReplyReport { get; internal set; }
        public string CallAGrid { get; internal set; }
        public string CallBGrid { get; internal set; }

        public string CallA { get; set; }
        public string CallB { get; set; }

        public DateTime DateTime { get; set; }
        public string LogLine { get; set; }
    }

    static class Extensions
    { 
        public static string LastBut(this IEnumerable<string> str, int but)
        {
            var items = str.ToArray();
            return items[items.Length - 1 - but];
        }
    }
}