using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CLIQUE;
#pragma warning disable CS1591

public class CLIQUEGadget : IGadget {

    private string _reductionType;

    private string _problemType;

    private string _gadgetString;

    private int _uniqueId; //NEEDED FOR SERIALIZATION!!!

    public CLIQUEGadget(string reductionType, string gadgetString, int id) {
        _reductionType = reductionType;
        _gadgetString = gadgetString;
        _problemType = "CLIQUE";
        _uniqueId = id;
    }


    public string reductionType {
        get {
            return _reductionType;
        }
        set {
            _reductionType = value;
        }
    }

    public string problemType {
        get {
            return _problemType;
        }
        set {
            _problemType = value;
        }
    }

    public string gadgetString {
        get {
            return _gadgetString;
        }
        set {
            _gadgetString = value;
        }
    }

    public int uniqueId {
        get {
            return _uniqueId;
        }
        set {
            _uniqueId = value;
        }
    }

    override public string? ToString() {

        return _gadgetString;
    }

}