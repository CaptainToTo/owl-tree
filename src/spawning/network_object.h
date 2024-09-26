
#ifndef OWLTREE_NETWORK_OBJECT_H
#define OWLTREE_NETWORK_OBJECT_H

#include "../ids/client_id.h";
#include "../ids/network_id.h";
#include "../rpcs/rpc_id.h";
#include "../encodable.h";
#include "../rpcs/rpc.h"
#include <stdint.h>
#include <string>

namespace owltree {

class network_object {
    public:
        typedef void (*action)(network_object*); 
        typedef void (*rpc_call)(rpc_caller, rpc_id, network_id, rpc_args, int);
    
    private:
        network_id _id;
        bool _is_active;

    public:
        rpc_call on_rpc_call;

        network_object(network_id id) {
            _id = id;
            _is_active = false;
        }

        network_object() {
            _id = network_id::none();
            _is_active = false;
        }

        void set_id_INTERNAL(network_id id) {
            _id = id;
        }

        void set_active_INTERNAL(bool state) {
            _is_active = state;
        }

        virtual void on_spawn() { }
        virtual void on_despawn() { }
};

}

#endif