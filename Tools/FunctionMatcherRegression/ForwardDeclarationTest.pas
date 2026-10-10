unit ForwardDeclarationTest;

interface

type
  TExample = class;

  TExample = class
  public
    function GetValue: Integer;
  end;

implementation

function TExample.GetValue: Integer;
begin
  Result := 42;
end;

end.
