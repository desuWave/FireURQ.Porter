unit PropertyTest;

interface

type
  TTest = class
  private
    FValue: Integer;
    FName: string;
  public
    property Value: Integer read FValue write FValue;
    property Name: string read FName write FName;
  end;

implementation

end.
