# Quickstart

> Note, this quickstart assumes that you have already installed NEO-Express
> via the instructions in the [project readme](../readme.md#installation)

## Create a Development Blockchain 

Create a new directory to work in. NEO-Express does not require an empty directory,
but it makes things simpler for the purposes of this quickstart. The shell snippets
in this section will use the directory ~/quickstart.

Start by creating a new NEO-Express blockchain instance with the create command.
While we're at it, lets create a standard wallet account with the wallet create
command.

``` shell
$ neo-express create
Created 1 node privatenet at ~/quickstart/express.privatenet.json
    Note: The private keys for the accounts in this file are are *not* encrypted.
          Do not use these accounts on MainNet or in any other system where security is a concern.
$ neo-express wallet create testWallet
testWallet
        AZDRabBcW4eESVfh9ssLd6udza9xpPn1P6
    Note: The private keys for the accounts in this wallet are *not* encrypted.
          Do not use these accounts on MainNet or in any other system where security is a concern.
```

Note the warning - NEO-Express does not encrypt private keys of wallet accounts.
Rather than forcing developers to manage passwords during development - which
typically results in the use of easy-to-guess passwords anyway - NEO-Express does
away with wallet password management entirely. Since these accounts are not secure,
NEO-Express reminds you to only use these accounts in development.

NEO-Express stores all the information about the blockchain instance in the
express.privatenet.json file. If you look in this file right now, you'll see
information about the single consensus node for this blockchain as well testWallet
account we created.

> Note, do not modify the express.privatenet.json file by hand!

By default, NEO-Express creates a single node blockchain. For development purposes,
a single node blockchain is often preferred. Furthermore, the checkpoint features
of NEO-Express only work on a single node blockchain. However, you can specify you
want a four or seven node blockchain with the --count option. For this quickstart,
we will stick with a single node blockchain.

## Start the Blockchain

Now that you have created the NEO-Express blockchain instance, you can run it.
The run command takes a zero-based node index argument. Since this is a one node
blockchain,  pass 0 for the index. The run command will continue logging new blocks
to the console until you shut down the node via Ctrl-C.  

``` shell
$ neo-express run 0
10:22:09.83 ConsensusService Info OnStart
10:22:10.22 ConsensusService Info initialize: height=1 view=0 index=0 role=Primary
10:22:10.30 ConsensusService Info persist block: height=0 hash=0x24235c6f52568aa3ac71812a1b8d38ae1e8d1e9083c898f22eaff904fd4676e8 tx=4
10:22:10.30 ConsensusService Info initialize: height=1 view=0 index=0 role=Primary
10:22:10.31 ConsensusService Info timeout: height=1 view=0
10:22:10.31 ConsensusService Info send prepare request: height=1 view=0
10:22:10.37 ConsensusService Info send commit
10:22:10.49 ConsensusService Info relay block: height=1 hash=0xc552f3c3eb32511155352d0b8cb9bb65fa66462484958251508fbf9b4400bf8d tx=1
10:22:13.41 ConsensusService Info persist block: height=1 hash=0xc552f3c3eb32511155352d0b8cb9bb65fa66462484958251508fbf9b4400bf8d tx=1
10:22:13.42 ConsensusService Info initialize: height=2 view=0 index=0 role=Primary
10:22:28.42 ConsensusService Info timeout: height=2 view=0
10:22:33.49 ConsensusService Info send prepare request: height=2 view=0
10:22:33.51 ConsensusService Info send commit
10:22:33.56 ConsensusService Info relay block: height=2 hash=0x57938b38590c79994ef2a48c68750dc0aff7e2e1fff8f3c32e82ecb3d49bd932 tx=1
10:22:33.57 ConsensusService Info persist block: height=2 hash=0x57938b38590c79994ef2a48c68750dc0aff7e2e1fff8f3c32e82ecb3d49bd932 tx=1
10:22:33.57 ConsensusService Info initialize: height=3 view=0 index=0 role=Primary
```

By default, NEO-Express generates a new block every fifteen seconds, just like
MainNet. However, for development purposes, it's often desirable to run the
blockchain much faster than that. The block generation period affects how quickly
developers can view results of operations like transfer and it affects how quickly
accounts accumulate GAS.

You can control the block generation period via the --seconds-per-block option
(-s for short) of the run command. So if you wanted your blockchain to generate a
block every second, the command would be `neo-express run 0 -s 1`. The speed at
which the blockchain runs can't change while it's running, but it doesn't need
to be consistent between runs. So you could run the blockchain at a second per
block fast to configure it but then shutdown and restart at fifteen seconds per
block to simulate the speed at which MainNet runs.

## Transfer Assets Between Wallets

Since this terminal window is running the blockchain, open another terminal
window in the same directory so you can interact with the running blockchain.
Let's start by transferring 1,000,000 genesis NEO tokens to the testWallet account.

``` shell
$ neo-express transfer neo 1000000 genesis testWallet
{
  "contract-context": {
    "type": "Neo.Network.P2P.Payloads.ContractTransaction",
    "hex": "8000000101f5b3ba27937f15f70e8206abdd3b868dfb9491d6d7b3434bd37804bf03d7380000029b7cffdaa674beae0f930ebe6085af9093e5fe56b34a5c220ccdcf6efc336fc500c0465fff2b230076827dc6a9fab8e3789baf0babcb70576a3025789b7cffdaa674beae0f930ebe6085af9093e5fe56b34a5c220ccdcf6efc336fc500407a10f35a0000fac3127f6713fc2370985ff9c55f30deb00ec71b",
    "items": {}
  },
  "script-hashes": [
    "ASaVhq5FodDSeGGxs2WjmR1FQKeyhTE1B8"
  ],
  "hash-data": "8000000101f5b3ba27937f15f70e8206abdd3b868dfb9491d6d7b3434bd37804bf03d7380000029b7cffdaa674beae0f930ebe6085af9093e5fe56b34a5c220ccdcf6efc336fc500c0465fff2b230076827dc6a9fab8e3789baf0babcb70576a3025789b7cffdaa674beae0f930ebe6085af9093e5fe56b34a5c220ccdcf6efc336fc500407a10f35a0000fac3127f6713fc2370985ff9c55f30deb00ec71b"
}
{
  "txid": "0x553c0e89d6c3d87f3b17ccbeaae1dc4a3fb54fcb04ffc1a1cebf9aefc7c7c4e5"
}
```

> Note, currently NEO-Express dumps JSON information about operations to the
> console. A future update will display results in a more user-friendly fashion.

NEO-Express allows you to refer to wallet account by an easy-to-remember names
instead of by Base58 encoded addresses like `AZDRabBcW4eESVfh9ssLd6udza9xpPn1P6`.
There are a few reserved names, such as 'genesis'. Genesis refers to the multi-
signature account that receives the genesis NEO created for every new blockchain.

We can see the result of our transfer via the show account command

``` shell
$ neo-express show account testWallet
{
  "version": 0,
  "script_hash": "0x1bc70eb0de305fc5f95f987023fc13677f12c3fa",
  "frozen": false,
  "votes": [],
  "balances": [
    {
      "asset": "0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b",
      "value": "1000000"
    }
  ]
}
```

> Note, you must wait for the next block to relay before you can see the results
> of the transfer. Running the blockchain faster than the fifteen second default
> means you don't have to wait as long for operations to complete!
