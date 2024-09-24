
#ifndef OWLTREE_NETWORK_BUFFER_H
#define OWLTREE_NETWORK_BUFFER_H

#include "../ids/client_id.h"
#include "../ids/network_id.h"
#include "../rpcs/rpc_id.h"
#include <string>

namespace owltree {

class network_buffer {    
    private:
        int _buffer_size;
        int _port;
        std::string _address;

    protected:
        bool _is_ready;
        client_id _local_id;
    
    public:
        client_id::action on_client_connected;
        client_id::action on_client_disconnected;
        client_id::action on_ready;

        int buffer_size() { return _buffer_size; }
        int port() { return _port; }
        const std::string& address() { return _address; }

        bool is_ready() { return _is_ready; }
        const client_id& local_id() { return _local_id; }

        virtual void read();
        virtual void send();
        virtual void disconnect();
        virtual void disconnect(client_id id);
};

}

#endif