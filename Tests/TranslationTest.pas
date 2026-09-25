unit TranslationTest;

interface

function Add(A, B: Integer): Integer;
function Calculate(A, B: Integer): Integer;
function Max(A, B: Integer): Integer;
function CountUp(A, B: Integer): Integer;
function SumTo(N: Integer): Integer;
function Multiply(A, B: Integer): Integer;
procedure SetValue(A: Integer);
function DescribeValue(A: Integer): Integer;
function CompareEqual(A, B: Integer): Integer;
function CompareNotEqual(A, B: Integer): Integer;
function LogicalAnd(A, B: Boolean): Boolean;
function LogicalOr(A, B: Boolean): Boolean;
function IntegerDiv(A, B: Integer): Integer;
function IntegerMod(A, B: Integer): Integer;
function LogicalNot(A: Boolean): Boolean;

var
  X: Integer;
  Enabled: Boolean;

const
  MaxValue = 100;

type
  TCounter = Integer;
  TState = (Idle, Running, Stopped);
  TValues = array[0..9] of Integer;
  TOffsetValues = array[1..5] of Integer;
  TPoint = record
    X: Integer;
    Y: Integer;
  end;
  PPoint = ^TPoint;
  TPointSet = set of Integer;
  TPointClassRef = class of TPoint;

implementation

function Add(A, B: Integer): Integer;
begin
  Result := A + B;
end;

function Calculate(A, B: Integer): Integer;
var
  C: Integer;
begin
  C := (A + B) * 2;
  Result := C;
end;

function Max(A, B: Integer): Integer;
begin
  if A > B then
    Result := A
  else
    Result := B;
end;

function CountUp(A, B: Integer): Integer;
begin
  while A < B do
    A := A + 1;

  Result := A;
end;

function SumTo(N: Integer): Integer;
var
  I: Integer;
begin
  Result := 0;

  for I := 1 to N do
    Result := Result + I;
end;

function Multiply(A, B: Integer): Integer;
begin
  Result := A * B;
end;

procedure SetValue(A: Integer);
begin
  X := A;
end;

function DescribeValue(A: Integer): Integer;
begin
  case A of
    0:
      Result := 10;

    1:
      Result := 20;

    2, 3:
      Result := 30;

  else
    Result := 40;
  end;
end;

function CompareEqual(A, B: Integer): Integer;
begin
  if A = B then
    Result := 1
  else
    Result := 0;
end;

function CompareNotEqual(A, B: Integer): Integer;
begin
  if A <> B then
    Result := 1
  else
    Result := 0;
end;

function LogicalAnd(A, B: Boolean): Boolean;
begin
  Result := A and B;
end;

function LogicalOr(A, B: Boolean): Boolean;
begin
  Result := A or B;
end;

function IntegerDiv(A, B: Integer): Integer;
begin
  Result := A div B;
end;

function IntegerMod(A, B: Integer): Integer;
begin
  Result := A mod B;
end;

function LogicalNot(A: Boolean): Boolean;
begin
  Result := not A;
end;

end.