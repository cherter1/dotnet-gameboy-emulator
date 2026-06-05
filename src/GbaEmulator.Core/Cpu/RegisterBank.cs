namespace GbaEmulator.Core.Cpu;

public sealed class RegisterBank(Func<CpuMode> getCurrentMode)
{
    private readonly uint[] _shared = new uint[16];

    private uint _spUserSystem;
    private uint _lrUserSystem;

    private uint _spIrq;
    private uint _lrIrq;

    private uint _spSvc;
    private uint _lrSvc;

    private ProgramStatusRegister _spsrIrq;
    private ProgramStatusRegister _spsrSvc;

    public uint this[int index]
    {
        get => index switch
        {
            13 => StackPointer,
            14 => LinkRegister,
            _ => _shared[index]
        };
        set
        {
            switch (index)
            {
                case 13:
                    StackPointer = value;
                    break;
                case 14:
                    LinkRegister = value;
                    break;
                default:
                    _shared[index] = value;
                    break;
            }
        }
    }

    public uint StackPointer
    {
        get => getCurrentMode() switch
        {
            CpuMode.Supervisor => _spSvc,
            CpuMode.Irq => _spIrq,
            _ => _spUserSystem
        };
        private set
        {
            switch (getCurrentMode())
            {
                case CpuMode.Supervisor:
                    _spSvc = value;
                    break;
                case CpuMode.Irq:
                    _spIrq = value;
                    break;
                case CpuMode.User:
                case CpuMode.System:
                case CpuMode.Fiq:
                case CpuMode.Abort:
                case CpuMode.Undefined:
                default:
                    _spUserSystem = value;
                    break;
            }
        }
    }
    public uint LinkRegister
    {
        get => getCurrentMode() switch
        {
            CpuMode.Supervisor => _lrSvc,
            CpuMode.Irq => _lrIrq,
            _ => _lrUserSystem
        };
        private set
        {
            switch (getCurrentMode())
            {
                case CpuMode.Supervisor:
                    _lrSvc = value;
                    break;
                case CpuMode.Irq:
                    _lrIrq = value;
                    break;
                case CpuMode.User:
                case CpuMode.System:
                case CpuMode.Fiq:
                case CpuMode.Abort:
                case CpuMode.Undefined:
                default:
                    _lrUserSystem = value;
                    break;
            }
        }
    }

    public uint ProgramCounter
    {
        get => _shared[15];
        set => _shared[15] = value;
    }

    public ProgramStatusRegister GetSpsr(CpuMode mode) => mode switch
    {
        CpuMode.Irq => _spsrIrq,
        CpuMode.Supervisor => _spsrSvc,
        _ => throw new InvalidOperationException($"Mode {mode} has no SPSR")
    };

    public void SetSpsr(CpuMode mode, ProgramStatusRegister value)
    {
        switch (mode)
        {
            case CpuMode.Irq:
                _spsrIrq = value;
                break;
            case CpuMode.Supervisor:
                _spsrSvc = value;
                break;
            default:
                throw new InvalidOperationException($"Mode {mode} has no SPSR");
        }
    }

    public void InitializeForGba()
    {
        // BIOS default stacks commonly used on GBA
        _spUserSystem = 0x03007F00;
        _spIrq = 0x03007FA0;
        _spSvc = 0x03007FE0;

        _lrUserSystem = 0;
        _lrIrq = 0;
        _lrSvc = 0;
    }
}