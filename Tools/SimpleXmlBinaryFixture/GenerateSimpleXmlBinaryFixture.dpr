program GenerateSimpleXmlBinaryFixture;

{$APPTYPE CONSOLE}

uses
  SysUtils, Variants, SimpleXML;

procedure CheckType(const Name: string; const Value: Variant; Expected: Word);
begin
  if VarType(Value) <> Expected then
    raise Exception.CreateFmt('%s: expected Variant type %d, got %d',
      [Name, Expected, VarType(Value)]);
end;

var
  FileName: string;
  Doc: IXmlDocument;
  Root: IXmlElement;
  Bytes: Variant;
  AnsiText: string;
  WideText: WideString;
begin
  try
    if ParamCount <> 1 then
      raise Exception.Create('Usage: GenerateSimpleXmlBinaryFixture <output-file>');

    FileName := ParamStr(1);
    Doc := CreateXmlDocument('variants');
    Root := Doc.DocumentElement;

    AnsiText := 'FireURQ';
    WideText := WideChar($0416);
    Bytes := VarArrayCreate([0, 2], varByte);
    Bytes[0] := 0;
    Bytes[1] := 128;
    Bytes[2] := 255;

    Root.Values['@integer'] := VarAsType(-123456789, varInteger);
    Root.Values['@double'] := VarAsType(12.5, varDouble);
    Root.Values['@boolean'] := True;
    Root.Values['@ansi'] := AnsiText;
    Root.Values['@wide'] := WideText;
    Root.Values['@bytes'] := Bytes;

    Doc.SaveBinary(FileName);
    Root := nil;
    Doc := LoadXmlDocument(FileName);
    if Doc = nil then
      raise Exception.Create('SimpleXML could not reopen the fixture');
    Root := Doc.DocumentElement;

    CheckType('integer', Root.Values['@integer'], varInteger);
    CheckType('double', Root.Values['@double'], varDouble);
    CheckType('boolean', Root.Values['@boolean'], varBoolean);
    CheckType('ansi', Root.Values['@ansi'], varString);
    CheckType('wide', Root.Values['@wide'], varOleStr);
    CheckType('bytes', Root.Values['@bytes'], varArray or varByte);

    Bytes := Root.Values['@bytes'];
    if (Root.Values['@integer'] <> -123456789) or
       (Root.Values['@double'] <> 12.5) or
       (Root.Values['@boolean'] <> True) or
       (Root.Values['@ansi'] <> AnsiText) or
       (Root.Values['@wide'] <> WideText) or
       (VarArrayLowBound(Bytes, 1) <> 0) or (VarArrayHighBound(Bytes, 1) <> 2) or
       (Bytes[0] <> 0) or (Bytes[1] <> 128) or (Bytes[2] <> 255) then
      raise Exception.Create('Unexpected fixture value after loading');

    Writeln('Original SimpleXML round trip passed: ', ExpandFileName(FileName));
  except
    on E: Exception do
    begin
      Writeln(E.ClassName, ': ', E.Message);
      ExitCode := 1;
    end;
  end;
end.
