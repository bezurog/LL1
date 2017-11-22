using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace lab2
{
    class LexReader // класс полексемного чтения входного файла 
    {

        public List<string> Lexems; //список лексем
        public List<string> Positions;//список позиций лексем
        public LexReader(string filename)
        {
            Lexems = new List<string>();
            Positions = new List<string>();
            Read(filename);
        }
        private void ins(string s, int line, int charpos)
        {
            Lexems.Add(s);
            Positions.Add(string.Format("{0}:{1}", line + 1, charpos));

        }
        public void Read(string filename) // загрузка
        {
            Lexems.Clear();
            Positions.Clear();
            if (!File.Exists(filename)) return;
            string[] l = File.ReadAllLines(filename);
            for (int i = 0; i < l.Length; i++) // по строкам
            {
                string str = Program.UpperCase ? l[i].ToUpper() : l[i];
                int p = 1;
                while (str != "")
                {
                    if (str[0] == ' ' || str[0] == '\t') //пропуск пробелов и табуляций
                    {
                        p += 1;
                        str = str.Substring(1);
                        continue;
                    }
                    Match m = Regex.Match(str, Program.UpperCase ? "^(@?([A-Z_][A-Z_0-9]*))" : "^(@?([A-Za-z_][A-Za-z_0-9.]*))");//имена
                    if (m.Success)
                    {
                        ins(m.Groups[1].Value, i, p);
                        p += m.Groups[1].Length;
                        str = str.Substring(m.Groups[1].Length);
                        continue;
                    }
                    m = Regex.Match(str, "^([0-9]+|\\$[0-9A-Fa-f]+)");//числа
                    if (m.Success)
                    {
                        ins(m.Groups[1].Value, i, p);
                        p += m.Groups[1].Length;
                        str = str.Substring(m.Groups[1].Length);
                        continue;
                    }
                    m = Regex.Match(str, "^(\\.+)"); // ..
                    if (m.Success)
                    {
                        ins(m.Groups[1].Value, i, p);
                        p += m.Groups[1].Length;
                        str = str.Substring(m.Groups[1].Length);
                        continue;
                    }
                    ins(str.Substring(0, 1), i, p); //остальные символы по отдельности
                    p += 1;
                    str = str.Substring(1);
                }

            }

            ins("", l.Length, 1); //признак конца файла - пустая строка
        }
    }

    class Rule  //правило грамматики
    {
        public string rname; // имя слева
        public List<string> rightnames; // справа от описания правила
        public List<string> startchars; //предшествующие символы
        public List<string> followchars; // последующие символы
        public List<string> action; //** действия
        public Rule(string s)
        {
            rname = s;
            rightnames = new List<string>();
            startchars = new List<string>();
            followchars = new List<string>();
            action = new List<string>(); //**

        }
        public List<string> termchars // направляющие символы. 
        {
            get
            {
                List<string> trm = new List<string>();
                foreach (string ss in startchars)
                    if (ss != "E" && !trm.Contains(ss))
                        trm.Add(ss); // все предшествующие кроме e
                if (startchars.Contains("E")) // если есть е в предш
                    foreach (string ss in followchars)
                        trm.Add(ss); // то и все последующие
                return trm;
            }
        }
        public void AddRight(string s)
        {
            rightnames.Add(s);
            action.Add(""); //** нет действие
        }
        public void AddAct(string act) //** задать действие
        {
            action[action.Count - 1] = act; //переписать
        }
    }

    class LL1Grammar // LL1 грамматика
    {
        public List<Rule> Grammar; // список всех правил грамматики
        public List<string> rulenames, regterms, regvals, terminals; // списки правил и терминалов


        public LL1Grammar(string FromFileName, bool debout)
        {
            rulenames = new List<string>();
            regterms = new List<string>();
            regvals = new List<string>();
            terminals = new List<string>();
            Grammar = new List<Rule>();
            if (LoadRules(FromFileName)) // загрузка правил
            {
                NormTermList();
                BuildStartChars();
                BuildFollowChars();
            }
            if (debout)
                DumpGrammar();
        }
        public bool valid { get { return Grammar.Count > 0 && CheckGrammar(); } }//проверка грамматики
        private bool CheckGrammar()//проверка грамматики
        {
            for (int i = 0; i < Grammar.Count; i++)
            {
                Rule op1 = Grammar[i];
                for (int j = i + 1; j < Grammar.Count; j++)
                {
                    Rule op2 = Grammar[j];
                    if (op1.rname != op2.rname) break;
                    List<string> both = op1.termchars.Intersect(op2.termchars).ToList(); // пересечение направляющих
                    if (both.Count != 0) //не должно пересекаться
                    {
                        string s = "";
                        foreach (string s1 in both) s += s1 + ",";
                        Console.WriteLine("Пересечение направляющих для {0} : [{1}]", op2.rname, s);
                        return false; // иначе грамматика не правильная
                    }
                }
            }
            return true;
        }

        private int IndexOfRule(string s) //индекс(первый) правила
        {
            for (int i = 0; i < Grammar.Count; i++)
                if (Grammar[i].rname == s) return i;
            return -1;
        }

        public bool IsRuleName(string rulename)// является ли правилом
        {
            return rulenames.IndexOf(rulename) != -1;
        }
        bool HasERuleFor(string rulename) // есть ли Е правило в правой части правил
        {
            for (int i = 0; i < Grammar.Count; i++)
            {
                if (Grammar[i].rname == rulename) continue;
                foreach (string ss in Grammar[i].rightnames)
                    if (ss == "E")
                        return true;
            }
            return false;
        }
        public bool LoadRules(string FileName) //загрузка правил из фала
        {
            foreach (string cl in File.ReadAllLines(FileName))
            {
                Match m;
                string line = Program.UpperCase ? cl.ToUpper() : cl;
                if (line.Trim() == "") continue;
                if (line.Substring(0, 2) == "//") continue;
                m = Regex.Match(line, "^\\s*(\\S+)\\s*\\-\\>\\s*(.*)\\s*$");
                if (m.Success)
                {
                    string rs = m.Groups[1].Value;
                    line = m.Groups[2].Value;
                    Grammar.Add(new Rule(rs));
                    Program.listaddnew(rulenames, rs);
                    while (line != "")
                    {
                        m = Regex.Match(line, "^\\s*(\\S+)\\s*(.*)\\s*$");
                        if (!m.Success)
                        {
                            Grammar.Clear();
                            return false;
                        }
                        string s = m.Groups[1].Value;
                        line = m.Groups[2].Value;
                        if (s == "|")
                        {
                            Grammar.Add(new Rule(rs));
                            continue;
                        }
                        if (s[0] == '<') //** сохранить действие 
                        {
                            Grammar[Grammar.Count - 1].AddAct(s);
                            continue;
                        }
                        else
                        {
                            Grammar[Grammar.Count - 1].AddRight(s);
                            Program.listaddnew(terminals, s);

                        }
                    }
                }
                else
                {
                    Grammar.Clear();
                    return false;
                }

            }
            return true;
        }
        private void NormTermList() // определение что терминалы, что правила, что регулярки
        {
            int i = 0;
            for (i = 0; i < rulenames.Count; i++)
            {
                int j = terminals.IndexOf(rulenames[i]);
                if (j == -1) continue;
                terminals.RemoveAt(j);
            }
            int jj = terminals.IndexOf("E");
            if (jj != -1) terminals.RemoveAt(jj);
            i = 0;
            while (i < Grammar.Count)
            {
                if (Grammar[i].rightnames.Count == 1 && Grammar[i].rightnames[0][0] == '/')
                {
                    terminals.Add(Grammar[i].rname);
                    regterms.Add(Grammar[i].rname);
                    regvals.Add(Grammar[i].rightnames[0]);
                    rulenames.Remove(Grammar[i].rname);
                    Grammar.RemoveAt(i);
                }
                else i++;
            }

        }
        private void BuildStartChars() // построение предшествующих
        {
            while (true)
            {
                bool b = false;
                for (int i = 0; i < Grammar.Count; i++)
                {
                    string s = Grammar[i].rightnames[0];
                    if (!IsRuleName(s))
                    {
                        if (Program.listaddnew(Grammar[i].startchars, s)) b = true;
                    }
                    else
                    {
                        for (int j = 0; j < Grammar.Count; j++)
                        {
                            if (s != Grammar[j].rname) continue;
                            if (Grammar[j].startchars.Count == 0) continue;
                            if (Program.listmergenew(Grammar[i].startchars, Grammar[j].startchars))
                                b = true;
                        }
                    }
                }
                if (!b) break;
            }

        }
        private void BuildFollowChars() // построение последующих
        {
            for (int i = 0; i < Grammar.Count; i++)
            {
                if (IsRuleName(Grammar[i].rname))
                    Program.listaddnew(Grammar[i].followchars, "");
            }
            while (true)
            {
                bool b = false;
                for (int i = 0; i < Grammar.Count; i++)
                {
                    for (int j = 0; j < Grammar.Count; j++)
                    {
                        for (int p = 0; p < Grammar[j].rightnames.Count; p++)
                        {
                            if (Grammar[j].rightnames[p] == Grammar[i].rname && !(p == Grammar[j].rightnames.Count - 1))
                            {
                                if (IsRuleName(Grammar[j].rightnames[p + 1]))
                                {
                                    int t = IndexOfRule(Grammar[j].rightnames[p + 1]);
                                    if (Program.listmergenew(Grammar[i].followchars, Grammar[t].startchars))
                                        b = true;
                                    if (Program.listmergenew(Grammar[i].followchars, Grammar[t].followchars))
                                        b = true;
                                }
                                else
                                    if (Program.listaddnew(Grammar[i].followchars, Grammar[j].rightnames[p + 1]))
                                    {
                                        b = true;
                                        break;
                                    }
                            }
                            if (Grammar[j].rightnames[p] == Grammar[i].rname && i != j && (p == Grammar[j].rightnames.Count - 1))
                            {
                                if (HasERuleFor(Grammar[i].rname))
                                    if (Program.listmergenew(Grammar[i].followchars, Grammar[j].followchars))
                                        b = true;
                            }
                        }
                    }
                }
                if (!b) break;
            }

        }
        private void DumpGrammar()
        {
            Console.WriteLine("{0,-30} {1,-10} {2,-15} {3,-15} ", "P", "S(A)", "F(A)", "T(A)");
            for (int i = 0; i < Grammar.Count; i++)
            {
                string sp = string.Format("{0} {1} -> ", i + 1, Grammar[i].rname);
                foreach (string st in Grammar[i].rightnames) sp += st;
                string sa = "", sf = "", sta = "";
                foreach (string st in Grammar[i].startchars) sa += st + ",";
                foreach (string st in Grammar[i].followchars) sf += st + ",";
                foreach (string st in Grammar[i].termchars) sta += st + ",";
                Console.WriteLine("{0,-30} S={1,-10} F={2,-15} T={3,-15} ", sp, sa, sf, sta);
            }
        }
    }
    struct TableItem // элемент таблицы
    {
        public List<string> terms;
        public int jump;
        public bool accept;
        public bool stack;
        public bool Return;
        public bool error;
        public string action; //** действие в таблице
        public string _s1, _s2;
        public TableItem(string s1, string s2)
        {
            terms = new List<string>();
            _s1 = s1;
            _s2 = s2;
            jump = 0;
            accept = stack = Return = false;
            error = true;
            action = "";
        }
    };
    class Table // сама таблица для ll(1) грамматики
    {
        List<TableItem> Tbl = new List<TableItem>(); //список элементов
        public TableItem AtIndex(int index) { return Tbl[index]; }
        private void jump(int from, int to) // задать переход в элемент таблицы
        {
            TableItem t = Tbl[from];
            t.jump = to;
            Tbl[from] = t;
        }
        private void stack(int from, bool val) // задать помещение в стек
        {
            TableItem t = Tbl[from];
            t.stack = val;
            Tbl[from] = t;
        }
        private void Return(int from, bool val) // задать возврат
        {
            TableItem t = Tbl[from];
            t.Return = val;
            Tbl[from] = t;
        }
        private void error(int index, bool val) //задать ошибку
        {
            TableItem t = Tbl[index];
            t.error = val;
            Tbl[index] = t;
        }

        private void accept(int index, bool val) // задать завершение
        {
            TableItem t = Tbl[index];
            t.accept = val;
            Tbl[index] = t;
        }
        private void action(int index, string val) //** задать связанное действие 
        {
            TableItem t = Tbl[index];
            t.action = val;
            Tbl[index] = t;
        }
        public void Build(LL1Grammar Gr) // построить таблицу по грамматике
        {
            List<Rule> Rules = Gr.Grammar;
            List<int> ml = new List<int>();
            List<int> mr = new List<int>();
            string st = "";
            int k = 0, t = 0;
            int i = 0;
            while (i < Rules.Count)
            {
                st = Rules[i].rname;
                for (int j = i; j < Rules.Count; j++)
                {
                    if (Rules[i].rname != Rules[j].rname)
                        break;
                    Tbl.Add(new TableItem(Rules[j].rname, ""));
                    if (i == j)
                        k = Tbl.Count - 1;
                    Program.listmergenew(Tbl[Tbl.Count - 1].terms, Rules[j].termchars);
                    ml.Add(Tbl.Count - 1);
                    t = j - i + 1;
                    if (j > i)
                    {
                        error(Tbl.Count - 2, false);
                    }
                }

                for (int j = i; j < i + t; j++)
                {
                    if (Rules[i].rname != Rules[j].rname) break;
                    for (int p = 0; p < Rules[j].rightnames.Count; p++)
                    {
                        TableItem tt = new TableItem(Rules[i].rname, Rules[j].rightnames[p]);
                        Tbl.Add(tt);
                        if (p == 0) jump(k + j - i, Tbl.Count - 1);

                        if (Gr.IsRuleName(Rules[j].rightnames[p]))
                        {
                            for (int ttt = 0; ttt < Rules.Count; ttt++)
                            {
                                if (Rules[ttt].rname == Rules[j].rightnames[p])
                                    Program.listmergenew(Tbl[Tbl.Count - 1].terms, Rules[ttt].termchars);
                            }
                            stack(Tbl.Count - 1, p < Rules[j].rightnames.Count - 1);
                        }
                        else
                            if ((Rules[j].rightnames[p] != "E"))
                            {
                                Program.listaddnew(Tbl[Tbl.Count - 1].terms, Rules[j].rightnames[p]);
                                accept(Tbl.Count - 1, true);
                                Return(Tbl.Count - 1, p == Rules[j].rightnames.Count - 1);
                                action(Tbl.Count - 1, Rules[j].action[p]); //** переписать действие в таблицу
                            }
                            else
                            {
                                Rules[j].rightnames[p] = "E";
                                Program.listmergenew(Tbl[Tbl.Count - 1].terms, Rules[j].termchars);
                                Return(Tbl.Count - 1, p == Rules[j].rightnames.Count - 1);
                            }
                    }

                    mr.Add(Tbl.Count - 1);
                }
                while (i < Rules.Count && st == Rules[i].rname)
                    i++;
            }


            for (i = 0; i < Tbl.Count; i++)
            {
                bool b = false;
                for (int j = 0; j < ml.Count; j++)
                    if (ml[j] == i) b = true;
                if (Gr.IsRuleName(Tbl[i]._s2) && !b) // из прав части
                {
                    for (int j = 0; j < Tbl.Count; j++)
                        if (Tbl[j]._s1 == Tbl[i]._s2 && Tbl[j]._s2 == "")
                        {
                            jump(i, j);
                            break;
                        }
                }
                b = false;
                for (int j = 0; j < mr.Count; j++) if (mr[j] == i) b = true;
                if (!Gr.IsRuleName(Tbl[i]._s2) && Tbl[i]._s2 != "")  // из прав части
                {
                    if (b) jump(i, -1);
                    else jump(i, i + 1);
                }
            }

        }
    }

    //4 описание структур
    class recinfo
    {
        public List<string> name; // имена структур
        public int size; // размер
        public List<recinfo> subs; // вложенные элементы
        public recinfo(string nm)
        {
            name = new List<string>();
            name.Add(nm);
            size = 0;
            subs = new List<recinfo>();
        }
        public void addname(string s, int depth, bool addtolast) // добавить имя
        {
            if (depth > 1)
            { //внуть на нужную глубину
                subs[subs.Count - 1].addname(s, depth - 1, addtolast);
                return;
            }
            if (addtolast) // добавить имя
                subs[subs.Count - 1].name.Add(s); //добавление к именам в последний подэлемент
            else
                subs.Add(new recinfo(s)); // иначе добавление нового элемента

        }
        public void setsize(int sz, int depth) //задать размер (только для терминалов, не структур)
        {
            if (depth > 0) //вглубь
            {
                subs[subs.Count - 1].setsize(sz, depth - 1);
                return;
            }
            if (subs.Count == 0) // если это структура то ничего не надо
                size = sz; // записать для терминалов
        }
        public int recalc() // пересчитать
        {
            if (subs.Count == 0) return size; //вернуть размер терминала
            size = 0;
            for (int i = 0; i < subs.Count; i++)
                size += subs[i].recalc(); //сумма размеров всех вложенных элементов
            return size;
        }
        public void save(StreamWriter f, int depth) //сохранение в файл
        {
            if (depth > 0)
            {
                for (int i = 0; i < depth - 1; i++) f.Write("    ");
                foreach (string n in name)
                    f.WriteLine("{0} ", n);
             //   f.WriteLine(": {0}", size);
            }
            for (int i = 0; i < subs.Count; i++)
                subs[i].save(f, depth + 1);

        }
    }

    class Program
    {

        public static StreamWriter f;
        static private Table table;
        static private LL1Grammar grammar;
        static int lexindex = 0;
        static recinfo REC = new recinfo("");


        static int s2int(string s)
        {
            if (s[0] == '$')
                return int.Parse(s.Substring(1), System.Globalization.NumberStyles.HexNumber);
            return int.Parse(s);

        }
        static string fullname(List<List<string>> names, string s)
        {
            string f = "";
            foreach (List<string> l in names)
            {
                f += l[l.Count - 1] + ".";
            }

            f = f.Substring(0, f.Length - 1);
            return f;

        }
        static void setlastsize(List<KeyValuePair<string, int>> sizes, int size)
        {
            KeyValuePair<string, int> last = sizes[sizes.Count - 1];
            sizes[sizes.Count - 1] = new KeyValuePair<string, int>(last.Key, size);
            string name = last.Key;
            for (int i = sizes.Count - 2; i >= 0; i--)
            {
                string s = sizes[i].Key;
                if (s + "." != name.Substring(0, s.Length + 1)) break;
                sizes[i] = new KeyValuePair<string, int>(sizes[i].Key, sizes[i].Value + size);
            }
        }
        static public bool ExecuteChecks(LexReader tk, bool DEB) // запуск проверки
        {
            int i = 0;
            List<int> stack = new List<int>();
            string s;
            string name, redecl = "";
            List<List<string>> names = new List<List<string>>(); //** стек имен
            List<string> usertypes = new List<string>(); //** пользовательские типы 
            if (!C) names.Add(new List<string>()); //**добавление первого уровня
            int dim = 0;//, dim2 = 0;
            int mult = 1, size = 0;//4  множитель и базовый размер типа
            bool addtolast = false;//4  добавить новую запись или добавить имя к текущей
            stack.Add(-1);
            lexindex = 0;
            int minus = 1;
            name = tk.Lexems[lexindex];
            s = name;
            TableItem T = table.AtIndex(i);
            while (true)
            {
                if (DEB)
                {
                    Console.Write("i={0} '{1}' [ ", i + 1, name);
                    for (int u = 0; u < stack.Count; u++)
                        Console.Write(",{0}", stack[u] + 1);
                    Console.Write("] ");
                    Console.Write(" {{ ", i + 1, name);
                    for (int u = 0; u < T.terms.Count; u++)
                        Console.Write("'{0}' ", T.terms[u]);
                    Console.WriteLine("} ");
                }
                bool b = false;
                for (int p = 0; p < T.terms.Count; p++)
                {
                    string term = T.terms[p];
                    if (term == name) { b = true; break; }
                    if (term == "" && name == "{eof}") { b = true; break; }
                    if (grammar.regterms.Contains(term) && !grammar.terminals.Contains(name))
                    {
                        int spec = grammar.regterms.IndexOf(term);
                        term = grammar.regvals[spec];
                        string tmp = term.Substring(1, term.Length - 2);
                        b = Regex.IsMatch(name, tmp);
                        if (b) break;
                    }
                }
                if (b)
                {

                    if (T.action.Length > 0) //** обработка действий 
                    {
                        string act = T.action;
                        if (DEB)
                            Console.WriteLine("act {0}({1})\n", act, s);
                        if (act.ToUpper() == "<NEWVAR>") // новая переменная
                        {
                            if (s.Length > 8) s = s.Substring(0, 8);
                            if (names[names.Count - 1].Contains(s))
                                break; // есть на этом уровне
                            if(usertypes.LastOrDefault() == s)
                                break; //есть в пользовательских
                            names[names.Count - 1].Add(s);
                            REC.addname(s, names.Count, addtolast); //4 добавить имя
                            addtolast = false;//4 сбросить флаг нового имени
                        }
                        if (act.ToUpper() == "<NEWTYPE>") // новый тип
                        {
                            if (usertypes.LastOrDefault() == s)
                                break;
                            if (names.Count >= 2)
                                if (names[names.Count-2].Contains(s))
                                    break; // есть на этом уровне
                                       //if (usertypes.Contains(s)) break;//есть в пользовательских
                            usertypes.Add(s);
                            
                        }
                        if (act.ToUpper() == "<STRUCTIN>") //вход в запись
                        {
                            names.Add(new List<string>()); // новый уровень
                        }
                        if (act.ToUpper() == "<STRUCTOUT>") //выход
                        {
                            names.RemoveAt(names.Count - 1); // удалить 1 уровень
                        }
                        if (act.ToUpper() == "<STARTCALC>")
                        {
                            size = 0;
                            mult = 1;
                        }
                        if (act.ToUpper() == "<ADDNAME>") // новое имя через запятую в том же описании ( a,b:... )
                        {
                            addtolast = true;
                        }

                        if (act.ToUpper() == "<UPDSIZE>")  //4 сохранить подсчитанный размер
                        {
                            REC.setsize(size * mult, names.Count); // размер * множитель , глубина вложенности
                        }
                        if (act.ToUpper() == "<1>") //4 размер 1 байт
                        {
                            size = 1;

                        }
                        if (act == "<2>") //4 размер 2 байт
                        {
                            size = 2;

                        }
                        if (act == "<4>") //4 размер 4 байт
                        {
                            size = 4;

                        }
                        if (act == "<6>") //4 размер 6 байт
                        {
                            size = 6;

                        }
                        if (act == "<8>") //4 размер 8 байт
                        {
                            size = 8;

                        }
                        if (act == "<10>") //4 размер 8 байт
                        {
                            size = 10;

                        }
                        if (act == "<256>") // 4 строка 256 (может перезаписаться)
                        {
                            size = 256;

                        }
                        if (act.ToUpper() == "<MULT2>") //4 добавить множитель 2. размерность массива
                        {
                            mult *= 2;
                        }
                        if (act.ToUpper() == "<MULT256>") //4 *256
                        {
                            mult *= 256;
                        }
                        if (act.ToUpper() == "<MULT65535>") //4 *2 байт
                        {
                            mult *= 65535;
                        }


                        if (act.ToUpper() == "<C256>") //проверка длины строки
                        {
                            int sz;
                            sz = s2int(s);
                            if (sz < 1 || sz > 255) break;
                            size = sz + 1;//4 обновление размера для строки

                        }
                        if (act.ToUpper() == "<MINUS>") minus = -1;

                        if (act.ToUpper() == "<DIM>")
                        {
                            dim = s2int(s) * minus; // сохранить первую размерность массива
                            if (dim <= 0) break; //диапазон не правильный 
                            mult *= dim;
                            minus = 1;
                        }
                        /*
                        if (act == "<DIM2>") // вторая размерность
                        {
                            dim2 = s2int(s) * minus;
                            if (dim1 > dim2) break; 
                            mult *= dim2 - dim1 + 1; //4 обновление множителя для диапазона 
                            minus = 1;
                        }*/
                    } //** обработка действий 


                    if (T.accept)
                    {
                        lexindex++;
                        name = lexindex < tk.Lexems.Count ? tk.Lexems[lexindex] : "";
                        s = name;
                        if (DEB)
                            Console.WriteLine("read '{0}'\n", name);
                        //				chvar=false;
                    }
                    if (T.stack)
                        stack.Add(i);
                    if (T.Return)
                    {
                        if (stack.Count == 0)
                        {
                            break;
                        }
                        i = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        if (i == -1) break;
                        i++;
                        T = table.AtIndex(i);
                        continue;
                    }
                    if (T.jump != -1)
                    {
                        i = T.jump;
                        T = table.AtIndex(i);
                        continue;
                    };
                }
                else
                {
                    if (!T.error)
                    {
                        i++;
                        T = table.AtIndex(i);
                        continue;
                    }
                    else
                        break;
                }
            }

            return stack.Count == 0;

        }
        public static bool listaddnew(List<string> l, string newstr)
        {
            if (l.Contains(newstr)) return false;
            l.Add(newstr);
            return true;

        }
        public static bool listmergenew(List<string> tol, List<string> froml)
        {
            bool n = false;
            foreach (string s in froml)
                if (listaddnew(tol, s)) n = true;

            return n;
        }

        static void Main(string[] args)
        {

            f = new StreamWriter("output.txt", false);
            f.AutoFlush = true;

            grammar = new LL1Grammar("grammar.txt", false); // создать грамматику
            if (grammar.valid) //проверить грамматику
            {
                LexReader lexs = new LexReader("input.txt"); //загрузить лексемы 
                table = new Table();
                table.Build(grammar);//построить таблицу по грамматике
                if (ExecuteChecks(lexs, true)) //запуск проверки
                {
                    f.WriteLine("Ошибок не найдено");
                    REC.recalc(); //4 пересчет размеров структур
                    REC.save(f, 0);//4 вывод размеров структур
                }
                else
                {
                    f.WriteLine("Ошибка в позиции {0}", lexs.Positions[lexindex]);
                }
            }
            else f.WriteLine("Обнаружена ошибка в описании грамматики");
            Console.ReadKey();
        }
        public const bool UpperCase = false;
        public const bool C = true; 

    }
}
