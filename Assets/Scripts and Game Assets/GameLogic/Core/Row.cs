using System.Collections.Generic;

public class Row {
    public RowType RowType;
    public List<Unit> Units = new List<Unit>();

    public Row(RowType type) {
        RowType = type;
    }
}
