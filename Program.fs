// Learn more about F# at https://fsharp.org
// See the 'F# Tutorial' project for more help.
open System
open System.IO

[<Literal>]
let inFolder = @"E:\giulio-vs-so\upload\code\"
[<Literal>]
let outFolder = @"E:\giulio-vs-so\upload\code\output\"

let csv_input = inFolder + "CodeLTExceesds.csv"

type CsvType = {Name: string; Desc: string; Field: string}

let readCsv (path: string) (delim:char) (name_idx:int) (desc_idx:int) (field_idx:int) : CsvType[] =
    [|
        use sr = new StreamReader(path)
        while (not sr.EndOfStream) do
            let cols = sr.ReadLine().Split(delim)
            yield {
                Name = cols.[name_idx].Trim(); 
                Desc = cols.[desc_idx].Trim(); 
                Field = 
                    cols.[field_idx].Trim()
                        .Replace("?"," option")
            }
    |]

let DB_Model_out = outFolder + "DB_Model.fs"
let DB_Model_01begin = @"    [<WebSharper.JavaScript>]
    type AlertLTExceed = { 
        BookCompany: string; 
        AlertCode: string; AlertKey: string; AlertEntity: string;
        Status: string; AssignedTo: string;  "
let DB_Model_01end = "    }"

let produceCode 
    (path: string)
    (produceCodeField: StreamWriter -> int -> CsvType -> unit) 
    (codeBegin: string)
    (codeEnd: string)
    =
    let lines = readCsv csv_input ';' 0 1 2
    use sw = new StreamWriter(path)
    sw.WriteLine codeBegin
    lines
    |>Array.iteri (produceCodeField sw)
    sw.Write codeEnd

let produceField (sw:StreamWriter) (i:int) (line: CsvType) =
    sw.WriteLine ("        " + line.Name + ": " + line.Field + ";")
let produceDbModel () =
    produceCode DB_Model_out produceField DB_Model_01begin DB_Model_01end


let Model_mapLT_out = outFolder + "Model_mapLT.fs"
let Model_mapLTbegin = @"
            BookCompany = ETS_SPA;
            AlertCode = A02;
            AlertKey = e.``Contratto ICTS``
            AlertEntity = ContrKey
            Status = Loaded;
            AssignedTo = """";"
let Model_mapLTend = "        }"
let prettySpace (desc: string) =
    if (desc.Contains(" "))
    then "e.``" + desc + "``"
    else "e." + desc
let assignDateTimeOption (i:int) (line: CsvType) =
        line.Name + // getDateFromExcel (e.TryGetValue 5 "LayCan Date End") 
        " = getDateFromExcel (e.TryGetValue  " + 
        i.ToString() +
        " \"" + line.Desc + "\")"
let assignDecimal (i:int) (line: CsvType) =
    line.Name + 
    " = decimal " + 
    (line.Desc |> prettySpace) 
let assignDecimalOption (i:int) (line: CsvType) =
    line.Name + 
    " = e.TryGetValue " + 
    i.ToString() +
    " \"" + line.Desc + "\"" + " |> getDecimalOptionFromExcel"
let assignString (i:int) (line: CsvType) =
    line.Name + 
    " = " + 
    (line.Desc |> prettySpace)   
let assignment (i:int) (line: CsvType) =
    match line.Field with
    | "decimal option" -> 
        assignDecimalOption i line
    | "decimal" -> 
        assignDecimal i line
    | "DateTime option" ->
        assignDateTimeOption i line 
    | "DateTime" ->
        assignDateTimeOption i line + " |> Option.defaultValue DateTime.Today"
    | _ ->
         assignString i line       
let produceMapField (sw:StreamWriter) (i:int) (line: CsvType) =
    sw.WriteLine 
        ("            " + 
         assignment i line
        )
let produceModelmapLT () =
    produceCode Model_mapLT_out produceMapField Model_mapLTbegin Model_mapLTend

let Client_showHeader = outFolder + "Client_showHeader.fs"
let Client_showHeaderBegin = @"
        tr [] ["
let Client_showHeaderEnd = "        ]"
let produceHeaderField (sw:StreamWriter) (i:int) (line: CsvType) =
    sw.WriteLine 
        (
         "            str2th " +
         "\"" + line.Desc + "\""
        )
let produceShowHeader () =
    produceCode 
        Client_showHeader produceHeaderField Client_showHeaderBegin Client_showHeaderEnd

let ClientTrSingleLinePath =  outFolder + "ClientTrSingleLine.fs"
let ClientTrSingleLineBegin = @"
        tr [] ["
let ClientTrSingleLineEnd = "        ]"
let produceTrSingleLineField (sw:StreamWriter) (i:int) (line: CsvType) =
   sw.WriteLine 
    (
    match line.Field with
    | "decimal" when line.Name.EndsWith("Perc") ->
        "          str2tr <| (float line." + line.Name + " * 100.).JS.ToFixed(0) + \"%\""
    | "decimal" ->
        "          str2tr <| (float line." + line.Name + ").JS.ToFixed(2)"
    | "decimal option" ->
        "          opt2tr (line." + line.Name + " |> Option.map string) "
    | "DateTime option" ->
        "          date2tr line." + line.Name
    | "DateTime" ->
        "          str2tr <| line." + line.Name + ".ToShortDateString()"
    | _ ->
        "          str2tr line." + line.Name
    )
let produceTrSingleLine () =
    produceCode 
        ClientTrSingleLinePath produceTrSingleLineField ClientTrSingleLineBegin ClientTrSingleLineEnd

let TablePath = outFolder + "Table.sql"
let TableBegin = @"
(
	AlertCode           VARCHAR (32) NOT NULL,
	BookCompany         VARCHAR (64) NOT NULL,
	AlertKey            VARCHAR (64) NOT NULL,
	AlertEntity         VARCHAR (32) NOT NULL,
	Status              VARCHAR (32) NOT NULL,"
let TableEnd = @"
	Message             NVARCHAR (max) NULL,
	AssignedTo          VARCHAR (32) NOT NULL,
	Outcome             NVARCHAR (max) NOT NULL,
	
	CONSTRAINT PK_LTAlert PRIMARY KEY (AlertCode, BookCompany, AlertKey)
	)"
let produceTableField (sw:StreamWriter) (i:int) (line: CsvType) =
   sw.WriteLine 
    (
    match line.Field with
    | "decimal option" ->
        "	" + line.Name + " DECIMAL (19, 4) NULL,"
    | "decimal" ->
         "	" + line.Name + " DECIMAL (19, 4) NOT NULL,"
    | "DateTime option" ->
         "	" + line.Name + " DATETIME NULL,"   
    | "DateTime" ->
         "	" + line.Name + " DATETIME NOT NULL,"
    | "string" -> // "string"
         "	" + line.Name + " VARCHAR (128) NOT NULL,"
    | unknow -> failwith "unknown column type: " + unknow
    )
let produceTable () =
    produceCode TablePath produceTableField TableBegin TableEnd

//Erasing Type Provider to F# Record Type
let ErasingTP2RecPath = outFolder + "TP2Rec.fs"
let ErasingTP2RecBegin = @"alerts
|> Seq.map (fun alert -> {
    AlertCode       = alert.AlertCode;
    AlertEntity     = alert.AlertEntity;
    AlertKey        = alert.AlertKey;
    Status          = alert.Status;
    AssignedTo      = alert.AssignedTo;
    BookCompany     = alert.BookCompany;
    Outcome         = alert.Outcome;
"
let ErasingTP2RecEnd = @"}"
let produceTP2RecField (sw:StreamWriter) (i:int) (line: CsvType) =
    sw.WriteLine ("    " + line.Name + " = alert." + line.Name + ";")
let produceTP2Rec () =
    produceCode ErasingTP2RecPath produceTP2RecField ErasingTP2RecBegin ErasingTP2RecEnd

////F# Record Type to erasing Type Provider
let Rec2ErasingTPpath = outFolder + "Rec2TP.fs"
let Rec2ErasingTPBegin = @"let insertMe = alertLTExceed.Create()
"
let Rec2ErasingTPEnd = "//insertMe. <- DateTime.UtcNow"
let produceRec2ErasingTPField (sw:StreamWriter) (i:int) (line: CsvType) =
    sw.WriteLine ("insertMe." + line.Name + " <- alert." + line.Name)
let produceRec2ErasingTP () =
    produceCode Rec2ErasingTPpath produceRec2ErasingTPField Rec2ErasingTPBegin Rec2ErasingTPEnd

[<EntryPoint>]
let main argv =
    produceDbModel ()
    produceModelmapLT ()
    produceShowHeader ()
    produceTrSingleLine ()
    produceTable ()
    produceTP2Rec ()
    produceRec2ErasingTP ()
    0 // return an integer exit code
