using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;

namespace Unbreakable.Authorization;

// De enige plaats in de API die een autorisatievraag stelt.
// De API beslist zelf niets meer: OpenFGA is de PDP.
public class Pdp
{
    public const string CanRead = "can_read";
    public const string CanDelete = "can_delete";

    private readonly OpenFgaClient _fga;

    public Pdp(IConfiguration cfg)
    {
        _fga = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = cfg["Fga:ApiUrl"],
            StoreId = cfg["Fga:StoreId"],
            AuthorizationModelId = cfg["Fga:ModelId"],
            Credentials = new Credentials
            {
                Method = CredentialsMethod.ApiToken,
                Config = new CredentialsConfig { ApiToken = cfg["Fga:ApiToken"] }
            }
        });
    }

    public async Task<bool> Check(string sub, string relation, int messageId)
    {
        var res = await _fga.Check(new ClientCheckRequest
        {
            User = "user:" + sub,
            Relation = relation,
            Object = "message:" + messageId
        });
        return res.Allowed ?? false;   // fail closed
    }

    // "Welke berichten mag ik zien?" in een enkele call.
    public async Task<List<int>> ListReadable(string sub)
    {
        var res = await _fga.ListObjects(new ClientListObjectsRequest
        {
            User = "user:" + sub,
            Relation = CanRead,
            Type = "message"
        });

        var ids = new List<int>();
        foreach (var o in res.Objects)
        {
            if (int.TryParse(o.Replace("message:", ""), out var id)) ids.Add(id);
        }
        return ids;
    }

    public Task WriteTuples(int messageId, string senderSub, string recipientSub)
        => _fga.Write(new ClientWriteRequest
        {
            Writes = new List<ClientTupleKey>
            {
                new ClientTupleKey { User = "user:" + senderSub,    Relation = "sender",    Object = "message:" + messageId },
                new ClientTupleKey { User = "user:" + recipientSub, Relation = "recipient", Object = "message:" + messageId }
            }
        });

    // Tuples mee opruimen, anders blijven rechten bestaan op een verdwenen object.
    public Task DeleteTuples(int messageId, string senderSub, string recipientSub)
        => _fga.Write(new ClientWriteRequest
        {
            Deletes = new List<ClientTupleKeyWithoutCondition>
            {
                new ClientTupleKeyWithoutCondition { User = "user:" + senderSub,    Relation = "sender",    Object = "message:" + messageId },
                new ClientTupleKeyWithoutCondition { User = "user:" + recipientSub, Relation = "recipient", Object = "message:" + messageId }
            }
        });
}
