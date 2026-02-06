namespace MEreservas.Services
{
    public class MasterPasswordService
    {

        private readonly string _masterPassword;

        public MasterPasswordService(IConfiguration config)
        {
            _masterPassword = config["MasterPassword"];
        }

        public bool Validate(string input)
        {
            return input == _masterPassword;
        }


    }
}
